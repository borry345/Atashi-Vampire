using System.Runtime.CompilerServices;

// The dev tests attach and detach dev mode through internal hooks instead of widening the public API.
[assembly: InternalsVisibleTo("Survivor.Tests.Dev")]
