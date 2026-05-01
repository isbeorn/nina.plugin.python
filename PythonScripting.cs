using NINA.Core.Model;
using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Interfaces;
using NINA.Core.Utility;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Equipment.Model;
using NINA.Image.ImageAnalysis;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Plugin.Python.PythonScriptingTestCategory;
using NINA.Plugin.Python.Properties;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Settings = NINA.Plugin.Python.Properties.Settings;

namespace NINA.Plugin.Python {

    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    ///
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "PythonScripting_Options" where PythonScripting corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class PythonScripting : PluginBase, INotifyPropertyChanged {
        private PluginOptionsAccessor pluginSettings;
        private IProfileService profileService;
        private string pythonSetupStatus = "Python setup has not been tested in this N.I.N.A. session.";
        private string interfaceLookupFilter = string.Empty;

        [ImportingConstructor]
        public PythonScripting(IProfileService profileService, IOptionsVM options, IImageSaveMediator imageSaveMediator) {
            // This helper class can be used to store plugin settings that are dependent on the current profile
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            this.profileService = profileService;
            var interfaceLookup = CreateInterfaceLookup();
            InterfaceLookup = CollectionViewSource.GetDefaultView(interfaceLookup);
            InterfaceLookup.Filter = FilterInterfaceLookup;
            TestPythonSetupCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(TestPythonSetup);
        }

        public ICommand TestPythonSetupCommand { get; }

        public ICollectionView InterfaceLookup { get; }

        public string InterfaceLookupFilter {
            get => interfaceLookupFilter;
            set {
                if (interfaceLookupFilter == value) {
                    return;
                }

                interfaceLookupFilter = value ?? string.Empty;
                InterfaceLookup.Refresh();
                RaisePropertyChanged();
            }
        }

        public string PythonSetupStatus {
            get => pythonSetupStatus;
            private set {
                if (pythonSetupStatus == value) {
                    return;
                }

                pythonSetupStatus = value;
                RaisePropertyChanged();
            }
        }

        public override Task Teardown() {
            PythonRuntimeManager.PrepareForApplicationShutdown();
            return base.Teardown();
        }

        private void TestPythonSetup() {
            try {
                var result = PythonRuntimeManager.TestSetup();
                PythonSetupStatus =
                    "Python setup test succeeded." + Environment.NewLine +
                    $"Python DLL: {result.PythonDllPath}" + Environment.NewLine +
                    $"Python executable: {result.PythonExecutable}" + Environment.NewLine +
                    $"Python version: {result.PythonVersion}";
            } catch (Exception ex) {
                PythonSetupStatus =
                    "Python setup test failed." + Environment.NewLine +
                    ex.Message + Environment.NewLine +
                    "Verify that 64-bit Python 3.12 is installed or set PYTHONNET_PYDLL to the full path of the Python DLL, then restart N.I.N.A.";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool FilterInterfaceLookup(object item) {
            if (string.IsNullOrWhiteSpace(InterfaceLookupFilter)) {
                return true;
            }

            if (item is not PythonInterfaceLookupEntry entry) {
                return false;
            }

            return entry.PythonName.Contains(InterfaceLookupFilter, StringComparison.OrdinalIgnoreCase)
                || entry.DotNetType.Contains(InterfaceLookupFilter, StringComparison.OrdinalIgnoreCase)
                || entry.Category.Contains(InterfaceLookupFilter, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(InterfaceLookupFilter, StringComparison.OrdinalIgnoreCase);
        }

        private static IList<PythonInterfaceMemberEntry> CreateMemberLookup(PythonInterfaceLookupEntry entry) {
            if (entry == null) {
                return new List<PythonInterfaceMemberEntry>();
            }

            Type type = entry.LookupType ?? ResolveLookupType(entry.DotNetType);
            if (type == null) {
                return new List<PythonInterfaceMemberEntry> {
                    new(
                        "Runtime value",
                        "Info",
                        entry.DotNetType,
                        entry.PythonName,
                        "This script value is available at runtime. Check the injected object in a running script with type(value) or dir(value) if you need runtime-specific members.")
                };
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            if (entry.IsTypeObject) {
                flags |= BindingFlags.Static;
            }

            var members = new List<PythonInterfaceMemberEntry>();

            if (entry.IsTypeObject) {
                foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.GetParameters().Length)) {
                    members.Add(new PythonInterfaceMemberEntry(
                        type.Name,
                        "Constructor",
                        $"{type.Name}({GetParameterSignature(constructor)})",
                        $"{entry.PythonName}({GetPythonParameterPlaceholder(constructor)})",
                        BuildConstructorAnnotation(type, constructor)));
                }

                foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public).OrderBy(x => x.Name)) {
                    members.Add(new PythonInterfaceMemberEntry(
                        nestedType.Name,
                        "Nested Type",
                        GetFriendlyTypeName(nestedType),
                        $"{entry.PythonName}.{nestedType.Name}",
                        $"Nested .NET type declared by {GetFriendlyTypeName(type)}. Access it through the injected type object when you need its constants or nested members."));
                }
            }

            foreach (var field in GetLookupFields(type, flags).OrderBy(x => x.Name)) {
                bool isStatic = field.IsStatic;
                string target = GetMemberUsageTarget(entry, isStatic);
                bool canWrite = !field.IsInitOnly && !field.IsLiteral;
                string usage = canWrite ? $"{target}.{field.Name} = value" : $"{target}.{field.Name}";

                members.Add(new PythonInterfaceMemberEntry(
                    field.Name,
                    field.IsLiteral ? "Constant" : "Field",
                    $"{GetFriendlyTypeName(field.FieldType)} {field.Name}",
                    usage,
                    BuildFieldAnnotation(entry, field, isStatic, canWrite)));
            }

            foreach (var property in GetLookupProperties(type, flags).OrderBy(x => x.Name)) {
                if (property.GetIndexParameters().Length > 0) {
                    continue;
                }

                bool isStatic = (property.GetMethod ?? property.SetMethod)?.IsStatic == true;
                bool canRead = property.GetMethod?.IsPublic == true;
                bool canWrite = property.SetMethod?.IsPublic == true;
                string target = GetMemberUsageTarget(entry, isStatic);
                string usage = canWrite
                    ? $"{target}.{property.Name} = value"
                    : $"{target}.{property.Name}";
                string access = canRead && canWrite ? "read/write" : canWrite ? "write-only" : "read-only";

                members.Add(new PythonInterfaceMemberEntry(
                    property.Name,
                    "Property",
                    $"{GetFriendlyTypeName(property.PropertyType)} {property.Name} {{ {access} }}",
                    usage,
                    BuildPropertyAnnotation(entry, property, isStatic, canRead, canWrite)));
            }

            foreach (var method in GetLookupMethods(type, flags).Where(x => !x.IsSpecialName && x.DeclaringType != typeof(object)).OrderBy(x => x.Name).ThenBy(x => x.GetParameters().Length)) {
                bool returnsTask = typeof(Task).IsAssignableFrom(method.ReturnType);
                bool isStatic = method.IsStatic;
                string target = GetMemberUsageTarget(entry, isStatic);
                string call = $"{target}.{method.Name}({GetPythonParameterPlaceholder(method)})";
                string usage = returnsTask ? $"{call}.Wait()" : call;

                members.Add(new PythonInterfaceMemberEntry(
                    method.Name,
                    "Method",
                    $"{GetFriendlyTypeName(method.ReturnType)} {method.Name}({GetParameterSignature(method)})",
                    usage,
                    BuildMethodAnnotation(entry, method, isStatic)));
            }

            foreach (var eventInfo in GetLookupEvents(type, flags).OrderBy(x => x.Name)) {
                bool isStatic = (eventInfo.AddMethod ?? eventInfo.RemoveMethod)?.IsStatic == true;
                string target = GetMemberUsageTarget(entry, isStatic);

                members.Add(new PythonInterfaceMemberEntry(
                    eventInfo.Name,
                    "Event",
                    $"{GetFriendlyTypeName(eventInfo.EventHandlerType)} {eventInfo.Name}",
                    $"{target}.{eventInfo.Name}",
                    BuildEventAnnotation(entry, eventInfo, isStatic)));
            }

            if (members.Count == 0) {
                members.Add(new PythonInterfaceMemberEntry(
                    "No documented public members",
                    "Info",
                    GetFriendlyTypeName(type),
                    entry.PythonName,
                    $"{GetFriendlyTypeName(type)} does not expose public members for this injected value."));
            }

            return members;
        }

        private static IEnumerable<PropertyInfo> GetLookupProperties(Type type, BindingFlags flags) {
            return GetLookupTypes(type)
                .SelectMany(x => x.GetProperties(flags))
                .GroupBy(x => $"{x.Name}|{GetFriendlyTypeName(x.PropertyType)}|{((x.GetMethod ?? x.SetMethod)?.IsStatic == true ? "static" : "instance")}")
                .Select(x => x.First());
        }

        private static IEnumerable<MethodInfo> GetLookupMethods(Type type, BindingFlags flags) {
            return GetLookupTypes(type)
                .SelectMany(x => x.GetMethods(flags))
                .GroupBy(GetMethodLookupKey)
                .Select(x => x.First());
        }

        private static IEnumerable<EventInfo> GetLookupEvents(Type type, BindingFlags flags) {
            return GetLookupTypes(type)
                .SelectMany(x => x.GetEvents(flags))
                .GroupBy(x => $"{x.Name}|{GetFriendlyTypeName(x.EventHandlerType)}")
                .Select(x => x.First());
        }

        private static IEnumerable<FieldInfo> GetLookupFields(Type type, BindingFlags flags) {
            return GetLookupTypes(type)
                .SelectMany(x => x.GetFields(flags))
                .Where(x => !x.IsSpecialName)
                .GroupBy(x => $"{x.Name}|{GetFriendlyTypeName(x.FieldType)}|{(x.IsStatic ? "static" : "instance")}")
                .Select(x => x.First());
        }

        private static IEnumerable<Type> GetLookupTypes(Type type) {
            if (type.IsInterface) {
                return new[] { type }.Concat(type.GetInterfaces());
            }

            return new[] { type };
        }

        private static string GetMethodLookupKey(MethodInfo method) {
            return $"{method.Name}|{GetFriendlyTypeName(method.ReturnType)}|{(method.IsStatic ? "static" : "instance")}|{string.Join(",", method.GetParameters().Select(x => GetFriendlyTypeName(x.ParameterType)))}";
        }

        private static string GetMemberUsageTarget(PythonInterfaceLookupEntry entry, bool isStatic) {
            if (!entry.IsTypeObject) {
                return entry.PythonName;
            }

            return isStatic ? entry.PythonName : "instance";
        }

        private static string GetParameterSignature(MethodBase method) {
            return string.Join(", ", method.GetParameters().Select(GetParameterSignature));
        }

        private static string GetPythonParameterPlaceholder(MethodBase method) {
            var parameters = method.GetParameters();
            return parameters.Length == 0 ? string.Empty : string.Join(", ", parameters.Select(x => x.Name));
        }

        private static string GetParameterSignature(ParameterInfo parameter) {
            string signature = $"{GetFriendlyTypeName(parameter.ParameterType)} {parameter.Name}";
            if (parameter.HasDefaultValue) {
                signature += $" = {FormatDefaultValue(parameter.DefaultValue)}";
            }

            return signature;
        }

        private static string FormatDefaultValue(object value) {
            return value switch {
                null => "null",
                string text => $"\"{text}\"",
                bool flag => flag ? "true" : "false",
                _ => value.ToString()
            };
        }

        private static string BuildConstructorAnnotation(Type type, ConstructorInfo constructor) {
            var parts = new List<string> {
                $"Creates a new {GetFriendlyTypeName(type)} instance."
            };

            AddParameterGuidance(parts, constructor.GetParameters());
            return string.Join(" ", parts);
        }

        private static string BuildFieldAnnotation(PythonInterfaceLookupEntry entry, FieldInfo field, bool isStatic, bool canWrite) {
            var access = canWrite ? "read/write" : "read-only";
            var parts = new List<string> {
                $"{(field.IsLiteral ? "Constant" : "Field")} declared on {GetFriendlyTypeName(field.DeclaringType)} with {access} access from Python."
            };

            if (entry.IsTypeObject && !isStatic) {
                parts.Add($"Create an instance first, for example instance = {entry.PythonName}(...), then use instance.{field.Name}.");
            }

            return string.Join(" ", parts);
        }

        private static string BuildPropertyAnnotation(PythonInterfaceLookupEntry entry, PropertyInfo property, bool isStatic, bool canRead, bool canWrite) {
            string access = canRead && canWrite ? "read/write" : canWrite ? "write-only" : "read-only";
            var parts = new List<string> {
                $"{access} {GetFriendlyTypeName(property.PropertyType)} property declared on {GetFriendlyTypeName(property.DeclaringType)}."
            };

            if (entry.IsTypeObject && !isStatic) {
                parts.Add($"Create an instance first, for example instance = {entry.PythonName}(...), then use instance.{property.Name}.");
            }

            if (!canWrite) {
                parts.Add("Use it to inspect state or retrieve a related .NET object; assign through another API if no setter is exposed.");
            } else if (canRead) {
                parts.Add("Read it with dot access or assign a compatible Python/.NET value.");
            } else {
                parts.Add("Assign a compatible Python/.NET value; the property does not expose a public getter.");
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string)) {
                parts.Add("The value is enumerable; iterate it from Python when pythonnet can expose the underlying collection.");
            }

            if (property.Name.Contains("Profile", StringComparison.OrdinalIgnoreCase)) {
                parts.Add("Profile-related values affect the active N.I.N.A. configuration, so avoid changing them in a running sequence unless that is intentional.");
            }

            return string.Join(" ", parts);
        }

        private static string BuildMethodAnnotation(PythonInterfaceLookupEntry entry, MethodInfo method, bool isStatic) {
            var parts = new List<string>();
            Type returnType = method.ReturnType;

            if (returnType == typeof(void)) {
                parts.Add("Synchronous call; returns no value.");
            } else if (typeof(Task).IsAssignableFrom(returnType)) {
                if (returnType.IsGenericType) {
                    parts.Add($"Asynchronous call returning {GetFriendlyTypeName(returnType.GetGenericArguments()[0])}; call .Wait() before using completion-dependent state, then read .Result if you need the returned value.");
                } else {
                    parts.Add("Asynchronous call returning Task; call .Wait() before depending on completion.");
                }
            } else if (IsAsyncEnumerable(returnType)) {
                parts.Add($"Returns a .NET async stream of {GetFriendlyTypeName(returnType.GetGenericArguments()[0])}. Consuming IAsyncEnumerable from Python is advanced; prefer Task-returning APIs unless you need streaming data.");
            } else {
                parts.Add($"Returns {GetFriendlyTypeName(returnType)}.");
            }

            if (entry.IsTypeObject && !isStatic) {
                parts.Add($"Create an instance first, for example instance = {entry.PythonName}(...), then call instance.{method.Name}(...).");
            }

            AddParameterGuidance(parts, method.GetParameters());
            AddNameGuidance(parts, method.Name);
            return string.Join(" ", parts);
        }

        private static string BuildEventAnnotation(PythonInterfaceLookupEntry entry, EventInfo eventInfo, bool isStatic) {
            var parts = new List<string> {
                $"Event declared on {GetFriendlyTypeName(eventInfo.DeclaringType)} with handler type {GetFriendlyTypeName(eventInfo.EventHandlerType)}."
            };

            if (entry.IsTypeObject && !isStatic) {
                parts.Add($"Create an instance first, for example instance = {entry.PythonName}(...), then subscribe to instance.{eventInfo.Name}.");
            }

            parts.Add("Subscribing from an instruction script is advanced because the handler lifetime must be managed; prefer direct method calls unless the script controls unsubscribe behavior.");
            return string.Join(" ", parts);
        }

        private static bool IsAsyncEnumerable(Type type) {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
        }

        private static void AddParameterGuidance(ICollection<string> parts, ParameterInfo[] parameters) {
            if (parameters.Length == 0) {
                parts.Add("No arguments are required.");
                return;
            }

            var parameterNotes = new List<string>();
            foreach (var parameter in parameters) {
                Type parameterType = parameter.ParameterType;
                Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType() : parameterType;

                if (effectiveType == typeof(CancellationToken)) {
                    parameterNotes.Add($"{parameter.Name}: pass the injected token so sequence cancellation can stop the operation.");
                } else if (effectiveType == typeof(IProgress<ApplicationStatus>)) {
                    parameterNotes.Add($"{parameter.Name}: pass the injected progress object so N.I.N.A. can display status updates.");
                } else if (effectiveType == typeof(CaptureSequence)) {
                    parameterNotes.Add($"{parameter.Name}: pass captureSequence or a CaptureSequence you constructed and configured.");
                } else if (effectiveType == typeof(PrepareImageParameters)) {
                    parameterNotes.Add($"{parameter.Name}: pass prepareImageParameters or a new PrepareImageParameters(autoStretch, detectStars).");
                } else if (effectiveType == typeof(IProfile) || effectiveType?.Name?.EndsWith("Settings", StringComparison.Ordinal) == true) {
                    parameterNotes.Add($"{parameter.Name}: profile/settings object; changes can affect N.I.N.A. configuration.");
                } else if (parameter.HasDefaultValue) {
                    parameterNotes.Add($"{parameter.Name}: optional parameter; omit it to use the .NET default.");
                } else if (effectiveType?.IsEnum == true) {
                    parameterNotes.Add($"{parameter.Name}: pass a {GetFriendlyTypeName(effectiveType)} enum value.");
                }
            }

            if (parameterNotes.Count > 0) {
                parts.Add(string.Join(" ", parameterNotes));
            } else {
                parts.Add("Pass values compatible with the listed .NET parameter types; pythonnet performs normal conversions where possible.");
            }
        }

        private static void AddNameGuidance(ICollection<string> parts, string methodName) {
            if (methodName.Contains("Connect", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Disconnect", StringComparison.OrdinalIgnoreCase)) {
                parts.Add("This changes equipment connection state.");
            } else if (methodName.Contains("Capture", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Exposure", StringComparison.OrdinalIgnoreCase)) {
                parts.Add("This participates in camera exposure/capture workflow.");
            } else if (methodName.Contains("Slew", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Move", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Rotate", StringComparison.OrdinalIgnoreCase)) {
                parts.Add("This can move equipment; make sure the sequence state and safety checks are appropriate before calling it.");
            } else if (methodName.Contains("Abort", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Cancel", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Stop", StringComparison.OrdinalIgnoreCase)) {
                parts.Add("This stops or interrupts an operation.");
            } else if (methodName.Contains("Register", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Release", StringComparison.OrdinalIgnoreCase)) {
                parts.Add("This changes an internal registration/lifetime relationship; pair register and release calls carefully.");
            } else if (methodName.Contains("Save", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Remove", StringComparison.OrdinalIgnoreCase) || methodName.Contains("Delete", StringComparison.OrdinalIgnoreCase)) {
                parts.Add("This can persist or remove data.");
            }
        }

        private static Type ResolveLookupType(string dotNetType) {
            if (string.IsNullOrWhiteSpace(dotNetType)) {
                return null;
            }

            if (dotNetType == "System.TimeSpan") {
                return typeof(TimeSpan);
            }

            if (dotNetType == "System.Guid") {
                return typeof(Guid);
            }

            if (dotNetType == "CancellationToken") {
                return typeof(CancellationToken);
            }

            if (dotNetType == "IProgress<ApplicationStatus>") {
                return typeof(IProgress<ApplicationStatus>);
            }

            if (dotNetType.StartsWith("IPluggableBehaviorSelector<", StringComparison.Ordinal)) {
                string genericArgumentName = dotNetType.Substring("IPluggableBehaviorSelector<".Length).TrimEnd('>');
                var genericType = FindTypeByName("IPluggableBehaviorSelector`1");
                var genericArgument = FindTypeByName(genericArgumentName);
                return genericType != null && genericArgument != null ? genericType.MakeGenericType(genericArgument) : null;
            }

            return FindTypeByName(dotNetType);
        }

        private static Type FindTypeByName(string typeName) {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            } catch (ReflectionTypeLoadException ex) {
                return ex.Types.Where(x => x != null);
            }
        }

        private static string GetFriendlyTypeName(Type type) {
            if (type == null) {
                return string.Empty;
            }

            if (type.IsByRef) {
                return $"{GetFriendlyTypeName(type.GetElementType())}&";
            }

            if (type.IsArray) {
                return $"{GetFriendlyTypeName(type.GetElementType())}[]";
            }

            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null) {
                return $"{GetFriendlyTypeName(nullableType)}?";
            }

            if (type == typeof(void)) {
                return "void";
            }

            if (type == typeof(bool)) {
                return "bool";
            }

            if (type == typeof(byte)) {
                return "byte";
            }

            if (type == typeof(short)) {
                return "short";
            }

            if (type == typeof(int)) {
                return "int";
            }

            if (type == typeof(long)) {
                return "long";
            }

            if (type == typeof(float)) {
                return "float";
            }

            if (type == typeof(double)) {
                return "double";
            }

            if (type == typeof(decimal)) {
                return "decimal";
            }

            if (type == typeof(string)) {
                return "string";
            }

            if (!type.IsGenericType) {
                return type.Name;
            }

            string name = type.Name;
            int tickIndex = name.IndexOf('`');
            if (tickIndex >= 0) {
                name = name.Substring(0, tickIndex);
            }

            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
        }

        private static IList<PythonInterfaceLookupEntry> CreateInterfaceLookup() {
            return new List<PythonInterfaceLookupEntry> {
                CreateLookupEntry("profileService", typeof(IProfileService), "Profile", "Profile service injected by N.I.N.A."),
                CreateLookupEntry("profile", typeof(IProfile), "Profile", "Shortcut to profileService.ActiveProfile."),
                CreateLookupEntry("camera", typeof(ICameraMediator), "Equipment", "Camera mediator."),
                CreateLookupEntry("telescope", typeof(ITelescopeMediator), "Equipment", "Telescope mediator."),
                CreateLookupEntry("focuser", typeof(IFocuserMediator), "Equipment", "Focuser mediator."),
                CreateLookupEntry("filterWheel", typeof(IFilterWheelMediator), "Equipment", "Filter wheel mediator."),
                CreateLookupEntry("guider", typeof(IGuiderMediator), "Equipment", "Guider mediator."),
                CreateLookupEntry("rotator", typeof(IRotatorMediator), "Equipment", "Rotator mediator."),
                CreateLookupEntry("flatDevice", typeof(IFlatDeviceMediator), "Equipment", "Flat device mediator."),
                CreateLookupEntry("weatherData", typeof(IWeatherDataMediator), "Equipment", "Weather data mediator."),
                CreateLookupEntry("dome", typeof(IDomeMediator), "Equipment", "Dome mediator."),
                CreateLookupEntry("switch", typeof(ISwitchMediator), "Equipment", "Switch mediator."),
                CreateLookupEntry("safetyMonitor", typeof(ISafetyMonitorMediator), "Equipment", "Safety monitor mediator."),
                CreateLookupEntry("domeSynchronization", typeof(IDomeSynchronization), "Equipment", "Dome synchronization service."),
                CreateLookupEntry("domeFollower", typeof(IDomeFollower), "Equipment", "Dome follower service."),
                CreateLookupEntry("imaging", typeof(IImagingMediator), "Imaging", "Imaging mediator."),
                CreateLookupEntry("imageHistoryVM", typeof(IImageHistoryVM), "Imaging", "Image history view model."),
                CreateLookupEntry("deepSkyObjectSearchVM", typeof(IDeepSkyObjectSearchVM), "Imaging", "Deep sky object search view model."),
                CreateLookupEntry("imageSave", typeof(IImageSaveMediator), "Imaging", "Image save mediator."),
                CreateLookupEntry("framingAssistantVM", typeof(IFramingAssistantVM), "Imaging", "Framing Assistant view model."),
                CreateLookupEntry("imageControlVM", typeof(IImageControlVM), "Imaging", "Image control view model."),
                CreateLookupEntry("imageStatisticsVM", typeof(IImageStatisticsVM), "Imaging", "Image statistics view model."),
                CreateLookupEntry("imageDataFactory", typeof(IImageDataFactory), "Imaging", "Image data factory."),
                CreateLookupEntry("exposureDataFactory", typeof(IExposureDataFactory), "Imaging", "Exposure data factory."),
                CreateLookupEntry("application", typeof(IApplicationMediator), "Application", "Application mediator."),
                CreateLookupEntry("applicationStatusMediator", typeof(IApplicationStatusMediator), "Application", "Application status mediator."),
                CreateLookupEntry("resourceDictionary", typeof(IApplicationResourceDictionary), "Application", "Application resource dictionary."),
                CreateLookupEntry("messageBroker", typeof(IMessageBroker), "Application", "Plugin message broker."),
                CreateLookupEntry("symbolBroker", typeof(ISymbolBroker), "Sequencer", "Sequencer symbol broker."),
                CreateLookupEntry("templateLinkResolver", typeof(ITemplateLinkResolver), "Sequencer", "Template link resolver."),
                CreateLookupEntry("sequence", typeof(ISequenceMediator), "Sequencer", "Sequence mediator."),
                CreateLookupEntry("previousItem", typeof(ISequenceItem), "Sequencer Check Context", "Previous sequence item passed to a Python Condition or Python Trigger check. This can be None."),
                CreateLookupEntry("nextItem", typeof(ISequenceItem), "Sequencer Check Context", "Next sequence item passed to a Python Condition or Python Trigger check. This can be None."),
                CreateLookupEntry("parent", typeof(ISequenceContainer), "Sequencer Check Context", "Sequence container that owns the Python Condition or Python Trigger. This can be None before the item is attached."),
                CreateLookupEntry("condition", typeof(PythonScriptingCondition), "Condition Context", "The Python Condition object that is currently being checked."),
                CreateLookupEntry("phase", typeof(string), "Trigger Context", "Python Trigger timing phase. The value is \"before\" before the next item starts or \"after\" after the previous item completes."),
                CreateLookupEntry("trigger", typeof(PythonScriptingTrigger), "Trigger Context", "The Python Trigger object that is currently being checked."),
                CreateLookupEntry("triggerRunner", typeof(SequentialContainer), "Trigger Context", "Container that holds the Python Trigger's Triggered Instructions."),
                CreateLookupEntry("state", typeof(IDictionary<string, object>), "Trigger Context", "Per-trigger dictionary reset when the sequence block initializes. Use it for small predicate state between trigger checks."),
                CreateLookupEntry("__file__", typeof(string), "File Mode Context", "Resolved absolute script path injected only when the active script source is File. This value is absent for inline scripts."),
                CreateLookupEntry("optionsVM", typeof(IOptionsVM), "Application", "Application options view model."),
                CreateLookupEntry("nighttimeCalculator", typeof(INighttimeCalculator), "Astronomy", "Nighttime calculator."),
                CreateLookupEntry("twilightCalculator", typeof(ITwilightCalculator), "Astronomy", "Twilight calculator."),
                CreateLookupEntry("planetariumFactory", typeof(IPlanetariumFactory), "Astronomy", "Planetarium factory."),
                CreateLookupEntry("plateSolverFactory", typeof(IPlateSolverFactory), "Plate Solving", "Plate solver factory."),
                CreateLookupEntry("windowServiceFactory", typeof(IWindowServiceFactory), "Application", "Window service factory."),
                CreateLookupEntry("starDetectionSelector", typeof(IPluggableBehaviorSelector<IStarDetection>), "Pluggable Behavior", "Star detection selector."),
                CreateLookupEntry("starAnnotatorSelector", typeof(IPluggableBehaviorSelector<IStarAnnotator>), "Pluggable Behavior", "Star annotator selector."),
                CreateLookupEntry("meridianFlipVMFactory", typeof(IMeridianFlipVMFactory), "Sequencer", "Meridian flip view model factory."),
                CreateLookupEntry("autoFocusVMFactory", typeof(IAutoFocusVMFactory), "Imaging", "Auto focus view model factory."),
                CreateLookupEntry("progress", typeof(IProgress<ApplicationStatus>), "Instruction Helper", "Progress reporter passed to a Python Script instruction."),
                CreateLookupEntry("token", typeof(CancellationToken), "Instruction Helper", "Cancellation token for a Python Script instruction run."),
                CreateLookupEntry("captureSequence", typeof(CaptureSequence), "Instruction Helper", "New capture sequence helper object for Python Script instructions."),
                CreateLookupEntry("prepareImageParameters", typeof(PrepareImageParameters), "Instruction Helper", "New prepare image parameters helper object for Python Script instructions."),
                CreateLookupEntry("TimeSpan", "System.TimeSpan", typeof(TimeSpan), "Injected Type", "Common .NET time span type.", true),
                CreateLookupEntry("Guid", "System.Guid", typeof(Guid), "Injected Type", "Common .NET GUID type.", true),
                CreateLookupEntry("CaptureSequence", typeof(CaptureSequence), "Injected Type", "Capture sequence type.", true),
                CreateLookupEntry("Coordinates", typeof(Coordinates), "Injected Type", "Astrometry coordinates type.", true),
                CreateLookupEntry("TopocentricCoordinates", typeof(TopocentricCoordinates), "Injected Type", "Topocentric coordinates type.", true),
                CreateLookupEntry("SiderealShiftTrackingRate", typeof(SiderealShiftTrackingRate), "Injected Type", "Sidereal shift tracking rate type.", true),
                CreateLookupEntry("FilterInfo", typeof(FilterInfo), "Injected Type", "Filter information type.", true),
                CreateLookupEntry("PrepareImageParameters", typeof(PrepareImageParameters), "Injected Type", "Prepare image parameters type.", true)
            };
        }

        private static PythonInterfaceLookupEntry CreateLookupEntry(string pythonName, Type lookupType, string category, string description, bool isTypeObject = false) {
            return CreateLookupEntry(pythonName, GetFriendlyTypeName(lookupType), lookupType, category, description, isTypeObject);
        }

        private static PythonInterfaceLookupEntry CreateLookupEntry(string pythonName, string dotNetType, Type lookupType, string category, string description, bool isTypeObject = false) {
            var entry = new PythonInterfaceLookupEntry(pythonName, dotNetType, lookupType, category, description, isTypeObject);
            entry.Members = CreateMemberLookup(entry);
            return entry;
        }
    }

    public class PythonInterfaceLookupEntry {
        public PythonInterfaceLookupEntry(string pythonName, string dotNetType, Type lookupType, string category, string description, bool isTypeObject) {
            PythonName = pythonName;
            DotNetType = dotNetType;
            LookupType = lookupType;
            Category = category;
            Description = description;
            IsTypeObject = isTypeObject;
        }

        public string PythonName { get; }
        public string DotNetType { get; }
        public Type LookupType { get; }
        public string Category { get; }
        public string Description { get; }
        public bool IsTypeObject { get; }
        public IList<PythonInterfaceMemberEntry> Members { get; set; }
    }

    public class PythonInterfaceMemberEntry {
        public PythonInterfaceMemberEntry(string name, string kind, string signature, string usage, string annotation) {
            Name = name;
            Kind = kind;
            Signature = signature;
            Usage = usage;
            Annotation = annotation;
        }

        public string Name { get; }
        public string Kind { get; }
        public string Signature { get; }
        public string Usage { get; }
        public string Annotation { get; }
    }
}
