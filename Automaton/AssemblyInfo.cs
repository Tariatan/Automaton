using System.Runtime.CompilerServices;

#if BUILD_ON_WINDOWS
using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]
#endif
[assembly: InternalsVisibleTo("Automaton.Tests")]
[assembly: InternalsVisibleTo("Automaton.Tuning")]
