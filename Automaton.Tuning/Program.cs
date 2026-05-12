using OpenCvSharp;
using Automaton;
using Automaton.Detectors;

var path = @"141.png";
using var image = Cv2.ImRead(path);
var detector = new ErrorPopupDetector();
Console.WriteLine($"state={detector.DetectPopupState(image)}");
Console.WriteLine(detector.DescribeScores(image));