using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("9545363a-86d1-4ef5-80d4-c77b8fadcf74")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.3.0.1037")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/isbeorn/nina.plugin.python")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://www.patreon.com/stefanberg/")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Scripting, Python")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/isbeorn/nina.plugin.python/blob/main/CHANGELOG.md")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"Run Python scripts directly from the N.I.N.A. Advanced Sequencer.

## Sequencer Items

- **Python Script** runs inline or file-based automation code.
- **Python Condition** evaluates a script and reads the assigned `result` value.
- **Python Trigger** evaluates before or after sequence item boundaries and runs configured triggered instructions when `result` is truthy.

## Runtime Setup

The plugin uses pythonnet and requires a compatible 64-bit Python 3 installation. It can use an explicitly configured Python DLL via `PYTHONNET_PYDLL` or a virtual environment via `PYTHONNET_VENV`.

## Script Context

Scripts receive common N.I.N.A. services for profile, equipment, imaging, application, sequencer, plate solving, astronomy, and image data workflows. They also receive read-only snapshots of N.I.N.A. symbol broker values and functions as `symbols`, `symbolFunctions`, and direct `Category_Key` variables/functions, the `getSymbolProvider` helper for publishing custom symbols, plus in-scope sequencer variables as `variables`, direct `Var_name` aliases, and the `setVariable` helper. Scripts also receive helpers such as cancellation tokens, progress reporting, capture sequence helpers, condition or trigger context, `__file__` for file-based scripts, and selected .NET helper types.

The plugin does not ship CPython itself.")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]
