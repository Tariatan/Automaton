"""
Train a U-Net segmentation model for Project Discovery playfield analysis.

Prerequisites:
    pip install torch torchvision segmentation-models-pytorch albumentations opencv-python onnx

Usage:
    python train_segmentation.py

500–800 samples, focusing on variety over volume, covering different biological patterns
(sparse dots, dense blobs, multiple regions, single region, top-heavy, bottom-heavy).

Input:
    training/playfields/
        sample_001.png          <- playfield crop (RGB)
        sample_001.masked.png   <- binary mask (white = target regions)
        sample_002.png
        sample_002.masked.png
        ...

Output:
    training/model/discovery-segmentation.onnx
"""

import os
import glob
import random
from pathlib import Path

import cv2
import numpy as np
import torch
import torch.nn as nn
from torch.utils.data import Dataset, DataLoader, random_split
import segmentation_models_pytorch as smp
import albumentations as A

# ── Configuration ──────────────────────────────────────────────

MODEL_INPUT_SIZE = 672
ENCODER = "mobilenet_v2"
ENCODER_WEIGHTS = "imagenet"
BATCH_SIZE = 4
EPOCHS = 200
LEARNING_RATE = 1e-3
MIN_LEARNING_RATE = 1e-6
VALIDATION_SPLIT = 0.15
EARLY_STOPPING_PATIENCE = 30
SEED = 42

PLAYFIELDS_DIR = os.path.join(os.path.dirname(__file__), "playfields")
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "model")
ONNX_OUTPUT_PATH = os.path.join(OUTPUT_DIR, "discovery-segmentation.onnx")


# ── Dataset ────────────────────────────────────────────────────

class PlayfieldDataset(Dataset):
    def __init__(self, pairs, transform=None):
        self.pairs = pairs
        self.transform = transform

    def __len__(self):
        return len(self.pairs)

    def __getitem__(self, idx):
        image_path, mask_path = self.pairs[idx]

        image = cv2.imread(image_path, cv2.IMREAD_COLOR)
        image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        image = cv2.resize(image, (MODEL_INPUT_SIZE, MODEL_INPUT_SIZE))

        mask = cv2.imread(mask_path, cv2.IMREAD_GRAYSCALE)
        mask = cv2.resize(mask, (MODEL_INPUT_SIZE, MODEL_INPUT_SIZE), interpolation=cv2.INTER_NEAREST)
        mask = (mask > 127).astype(np.float32)

        if self.transform:
            augmented = self.transform(image=image, mask=mask)
            image = augmented["image"]
            mask = augmented["mask"]

        image = image.astype(np.float32) / 255.0
        image = np.transpose(image, (2, 0, 1))  # HWC -> CHW
        mask = np.expand_dims(mask, 0)  # HW -> 1HW

        return torch.from_numpy(image), torch.from_numpy(mask)


def find_pairs(playfields_dir):
    pairs = []
    image_files = sorted(glob.glob(os.path.join(playfields_dir, "*.png")))

    for image_path in image_files:
        name = os.path.basename(image_path)
        if "masked" in name.lower():
            continue

        base = os.path.splitext(name)[0]
        mask_path = os.path.join(playfields_dir, f"{base}.masked.png")
        if os.path.exists(mask_path):
            pairs.append((image_path, mask_path))

    return pairs


# ── Augmentation ───────────────────────────────────────────────

train_transform = A.Compose([
    A.HorizontalFlip(p=0.5),
    A.VerticalFlip(p=0.3),
    A.RandomRotate90(p=0.3),
    A.ShiftScaleRotate(shift_limit=0.05, scale_limit=0.1, rotate_limit=10, p=0.5,
                       border_mode=cv2.BORDER_CONSTANT, value=0, mask_value=0),
    A.OneOf([
        A.RandomBrightnessContrast(brightness_limit=0.2, contrast_limit=0.2, p=1.0),
        A.HueSaturationValue(hue_shift_limit=15, sat_shift_limit=25, val_shift_limit=20, p=1.0),
    ], p=0.7),
    A.GaussNoise(var_limit=(5.0, 25.0), p=0.3),
    A.GaussianBlur(blur_limit=(3, 5), p=0.2),
])


# ── Loss ───────────────────────────────────────────────────────

class BCEDiceLoss(nn.Module):
    def __init__(self, bce_weight=0.5):
        super().__init__()
        self.bce_weight = bce_weight
        self.bce = nn.BCEWithLogitsLoss()

    def forward(self, logits, targets):
        bce_loss = self.bce(logits, targets)

        probs = torch.sigmoid(logits)
        smooth = 1.0
        intersection = (probs * targets).sum(dim=(2, 3))
        union = probs.sum(dim=(2, 3)) + targets.sum(dim=(2, 3))
        dice_loss = 1.0 - ((2.0 * intersection + smooth) / (union + smooth)).mean()

        return self.bce_weight * bce_loss + (1.0 - self.bce_weight) * dice_loss


# ── Training ───────────────────────────────────────────────────

def train():
    random.seed(SEED)
    np.random.seed(SEED)
    torch.manual_seed(SEED)

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Device: {device}")

    pairs = find_pairs(PLAYFIELDS_DIR)
    if not pairs:
        print(f"No image/mask pairs found in {PLAYFIELDS_DIR}")
        print("Expected: <name>.png + <name>.masked.png")
        return

    print(f"Found {len(pairs)} training pairs")

    dataset = PlayfieldDataset(pairs, transform=train_transform)
    val_size = max(1, int(len(dataset) * VALIDATION_SPLIT))
    train_size = len(dataset) - val_size
    train_dataset, val_dataset = random_split(
        dataset, [train_size, val_size],
        generator=torch.Generator().manual_seed(SEED))

    # Validation set uses no augmentation
    val_dataset.dataset = PlayfieldDataset(pairs, transform=None)

    train_loader = DataLoader(train_dataset, batch_size=BATCH_SIZE, shuffle=True, num_workers=0, drop_last=False)
    val_loader = DataLoader(val_dataset, batch_size=BATCH_SIZE, shuffle=False, num_workers=0)

    print(f"Train: {train_size}, Validation: {val_size}")

    model = smp.Unet(
        encoder_name=ENCODER,
        encoder_weights=ENCODER_WEIGHTS,
        in_channels=3,
        classes=1,
        activation=None,  # raw logits — sigmoid applied in loss and export
    ).to(device)

    total_params = sum(p.numel() for p in model.parameters())
    print(f"Model parameters: {total_params:,}")

    criterion = BCEDiceLoss(bce_weight=0.5)
    optimizer = torch.optim.AdamW(model.parameters(), lr=LEARNING_RATE, weight_decay=1e-4)
    scheduler = torch.optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=EPOCHS, eta_min=MIN_LEARNING_RATE)

    best_val_loss = float("inf")
    patience_counter = 0

    for epoch in range(1, EPOCHS + 1):
        # ── Train ──
        model.train()
        train_loss = 0.0
        for images, masks in train_loader:
            images, masks = images.to(device), masks.to(device)
            optimizer.zero_grad()
            logits = model(images)
            loss = criterion(logits, masks)
            loss.backward()
            optimizer.step()
            train_loss += loss.item() * images.size(0)

        train_loss /= train_size

        # ── Validate ──
        model.eval()
        val_loss = 0.0
        val_dice = 0.0
        with torch.no_grad():
            for images, masks in val_loader:
                images, masks = images.to(device), masks.to(device)
                logits = model(images)
                loss = criterion(logits, masks)
                val_loss += loss.item() * images.size(0)

                probs = torch.sigmoid(logits)
                preds = (probs > 0.5).float()
                intersection = (preds * masks).sum(dim=(2, 3))
                union = preds.sum(dim=(2, 3)) + masks.sum(dim=(2, 3))
                dice = ((2.0 * intersection + 1.0) / (union + 1.0)).mean()
                val_dice += dice.item() * images.size(0)

        val_loss /= val_size
        val_dice /= val_size

        lr = optimizer.param_groups[0]["lr"]
        print(f"Epoch {epoch:3d}/{EPOCHS}  train_loss={train_loss:.4f}  val_loss={val_loss:.4f}  val_dice={val_dice:.4f}  lr={lr:.2e}")

        scheduler.step()

        if val_loss < best_val_loss:
            best_val_loss = val_loss
            patience_counter = 0
            os.makedirs(OUTPUT_DIR, exist_ok=True)
            torch.save(model.state_dict(), os.path.join(OUTPUT_DIR, "best_model.pth"))
        else:
            patience_counter += 1
            if patience_counter >= EARLY_STOPPING_PATIENCE:
                print(f"Early stopping at epoch {epoch} (no improvement for {EARLY_STOPPING_PATIENCE} epochs)")
                break

    # ── Export to ONNX ─────────────────────────────────────────
    print("Loading best model for ONNX export...")
    model.load_state_dict(torch.load(os.path.join(OUTPUT_DIR, "best_model.pth"), map_location=device, weights_only=True))
    model.eval()

    # Wrap model to include sigmoid in the exported graph
    class SigmoidWrapper(nn.Module):
        def __init__(self, base_model):
            super().__init__()
            self.base_model = base_model

        def forward(self, x):
            return torch.sigmoid(self.base_model(x))

    export_model = SigmoidWrapper(model).to(device)
    export_model.eval()

    dummy_input = torch.randn(1, 3, MODEL_INPUT_SIZE, MODEL_INPUT_SIZE, device=device)

    torch.onnx.export(
        export_model,
        dummy_input,
        ONNX_OUTPUT_PATH,
        input_names=["input"],
        output_names=["output"],
        dynamic_axes=None,
        opset_version=17,
    )

    onnx_size_mb = os.path.getsize(ONNX_OUTPUT_PATH) / (1024 * 1024)
    print(f"ONNX model exported: {ONNX_OUTPUT_PATH} ({onnx_size_mb:.1f} MB)")
    print(f"Input shape:  [1, 3, {MODEL_INPUT_SIZE}, {MODEL_INPUT_SIZE}]")
    print(f"Output shape: [1, 1, {MODEL_INPUT_SIZE}, {MODEL_INPUT_SIZE}]")
    print(f"Best val_loss: {best_val_loss:.4f}")
    print("Done. Copy the .onnx file to Automaton/Models/ to use it.")


if __name__ == "__main__":
    train()
