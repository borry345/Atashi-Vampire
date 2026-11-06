using System.Runtime.CompilerServices;

// Step-gate internals (TryBeginStep/EndStep/AbortStep, SimEntity.Bind) are driven directly by the loop tests.
[assembly: InternalsVisibleTo("Survivor.Tests.EditMode")]
[assembly: InternalsVisibleTo("Survivor.Tests.PlayMode")]
