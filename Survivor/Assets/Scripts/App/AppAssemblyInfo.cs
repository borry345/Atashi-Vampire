using System.Runtime.CompilerServices;

// The flow and assembly tests read the composition root's wiring (internal accessors) instead of widening the public API.
[assembly: InternalsVisibleTo("Survivor.Tests.EditMode")]
[assembly: InternalsVisibleTo("Survivor.Tests.PlayMode")]
