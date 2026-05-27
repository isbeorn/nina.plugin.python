using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Interfaces;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Equipment.Model;
using NINA.Image.ImageAnalysis;
using NINA.Image.Interfaces;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    public enum PythonTriggerTiming {
        BeforeNextItem,
        AfterCompletedItem,
        BeforeAndAfter
    }

    public sealed class PythonTriggerTimingOption {
        public PythonTriggerTimingOption(PythonTriggerTiming value, string label) {
            Value = value;
            Label = label;
        }

        public PythonTriggerTiming Value { get; }
        public string Label { get; }
    }

    [ExportMetadata("Name", "Python Trigger")]
    [ExportMetadata("Description", "Evaluates a Python predicate and runs the configured triggered instructions when result is truthy.")]
    [ExportMetadata("Icon", "Plugin_Test_SVG")]
    [ExportMetadata("Category", "Python Scripting")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class PythonScriptingTrigger : SequenceTrigger, IValidatable {
        private static readonly IReadOnlyList<PythonTriggerTimingOption> triggerTimingOptions = new[] {
            new PythonTriggerTimingOption(PythonTriggerTiming.BeforeNextItem, "Before next item"),
            new PythonTriggerTimingOption(PythonTriggerTiming.AfterCompletedItem, "After completed item"),
            new PythonTriggerTimingOption(PythonTriggerTiming.BeforeAndAfter, "Before and after")
        };

        private readonly IProfileService profileService;
        private readonly ICameraMediator cameraMediator;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IGuiderMediator guiderMediator;
        private readonly IRotatorMediator rotatorMediator;
        private readonly IFlatDeviceMediator flatDeviceMediator;
        private readonly IWeatherDataMediator weatherDataMediator;
        private readonly IImagingMediator imagingMediator;
        private readonly IApplicationStatusMediator applicationStatusMediator;
        private readonly INighttimeCalculator nighttimeCalculator;
        private readonly IPlanetariumFactory planetariumFactory;
        private readonly IImageHistoryVM imageHistoryVM;
        private readonly IDeepSkyObjectSearchVM deepSkyObjectSearchVM;
        private readonly IDomeMediator domeMediator;
        private readonly IImageSaveMediator imageSaveMediator;
        private readonly ISwitchMediator switchMediator;
        private readonly ISafetyMonitorMediator safetyMonitorMediator;
        private readonly IApplicationResourceDictionary resourceDictionary;
        private readonly IApplicationMediator applicationMediator;
        private readonly IFramingAssistantVM framingAssistantVM;
        private readonly IPlateSolverFactory plateSolverFactory;
        private readonly IWindowServiceFactory windowServiceFactory;
        private readonly IDomeFollower domeFollower;
        private readonly IPluggableBehaviorSelector<IStarDetection> starDetectionSelector;
        private readonly IPluggableBehaviorSelector<IStarAnnotator> starAnnotatorSelector;
        private readonly IImageDataFactory imageDataFactory;
        private readonly IMeridianFlipVMFactory meridianFlipVMFactory;
        private readonly IAutoFocusVMFactory autoFocusVMFactory;
        private readonly IImageControlVM imageControlVM;
        private readonly IImageStatisticsVM imageStatisticsVM;
        private readonly IDomeSynchronization domeSynchronization;
        private readonly ISequenceMediator sequenceMediator;
        private readonly IOptionsVM optionsVM;
        private readonly IExposureDataFactory exposureDataFactory;
        private readonly ITwilightCalculator twilightCalculator;
        private readonly IMessageBroker messageBroker;
        private readonly ISymbolBroker symbolBroker;
        private readonly ITemplateLinkResolver templateLinkResolver;
        private IList<string> issues = new List<string>();
        private IDictionary<string, object> state = new Dictionary<string, object>();
        private PythonScriptSource scriptSource = PythonScriptSource.Inline;
        private string scriptFilePath = string.Empty;

        [ImportingConstructor]
        public PythonScriptingTrigger(
            IProfileService profileService,
            ICameraMediator cameraMediator,
            ITelescopeMediator telescopeMediator,
            IFocuserMediator focuserMediator,
            IFilterWheelMediator filterWheelMediator,
            IGuiderMediator guiderMediator,
            IRotatorMediator rotatorMediator,
            IFlatDeviceMediator flatDeviceMediator,
            IWeatherDataMediator weatherDataMediator,
            IImagingMediator imagingMediator,
            IApplicationStatusMediator applicationStatusMediator,
            INighttimeCalculator nighttimeCalculator,
            IPlanetariumFactory planetariumFactory,
            IImageHistoryVM imageHistoryVM,
            IDeepSkyObjectSearchVM deepSkyObjectSearchVM,
            IDomeMediator domeMediator,
            IImageSaveMediator imageSaveMediator,
            ISwitchMediator switchMediator,
            ISafetyMonitorMediator safetyMonitorMediator,
            IApplicationResourceDictionary resourceDictionary,
            IApplicationMediator applicationMediator,
            IFramingAssistantVM framingAssistantVM,
            IPlateSolverFactory plateSolverFactory,
            IWindowServiceFactory windowServiceFactory,
            IDomeFollower domeFollower,
            IPluggableBehaviorSelector<IStarDetection> starDetectionSelector,
            IPluggableBehaviorSelector<IStarAnnotator> starAnnotatorSelector,
            IImageDataFactory imageDataFactory,
            IMeridianFlipVMFactory meridianFlipVMFactory,
            IAutoFocusVMFactory autoFocusVMFactory,
            IImageControlVM imageControlVM,
            IImageStatisticsVM imageStatisticsVM,
            IDomeSynchronization domeSynchronization,
            ISequenceMediator sequenceMediator,
            IOptionsVM optionsVM,
            IExposureDataFactory exposureDataFactory,
            ITwilightCalculator twilightCalculator,
            IMessageBroker messageBroker,
            ISymbolBroker symbolBroker,
            ITemplateLinkResolver templateLinkResolver
            ) {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.telescopeMediator = telescopeMediator;
            this.focuserMediator = focuserMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.guiderMediator = guiderMediator;
            this.rotatorMediator = rotatorMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.imagingMediator = imagingMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.nighttimeCalculator = nighttimeCalculator;
            this.planetariumFactory = planetariumFactory;
            this.imageHistoryVM = imageHistoryVM;
            this.deepSkyObjectSearchVM = deepSkyObjectSearchVM;
            this.domeMediator = domeMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.switchMediator = switchMediator;
            this.safetyMonitorMediator = safetyMonitorMediator;
            this.resourceDictionary = resourceDictionary;
            this.applicationMediator = applicationMediator;
            this.framingAssistantVM = framingAssistantVM;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.domeFollower = domeFollower;
            this.starDetectionSelector = starDetectionSelector;
            this.starAnnotatorSelector = starAnnotatorSelector;
            this.imageDataFactory = imageDataFactory;
            this.meridianFlipVMFactory = meridianFlipVMFactory;
            this.autoFocusVMFactory = autoFocusVMFactory;
            this.imageControlVM = imageControlVM;
            this.imageStatisticsVM = imageStatisticsVM;
            this.domeSynchronization = domeSynchronization;
            this.sequenceMediator = sequenceMediator;
            this.optionsVM = optionsVM;
            this.exposureDataFactory = exposureDataFactory;
            this.twilightCalculator = twilightCalculator;
            this.messageBroker = messageBroker;
            this.symbolBroker = symbolBroker;
            this.templateLinkResolver = templateLinkResolver;
            TriggerRunner = CreateAreaContainer();
            LoadScriptFromFileCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectScriptFile);
            RefreshScriptPreviewCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(RefreshScriptPreview);
        }

        private PythonScriptingTrigger(PythonScriptingTrigger copyMe) : this(
            copyMe.profileService,
            copyMe.cameraMediator,
            copyMe.telescopeMediator,
            copyMe.focuserMediator,
            copyMe.filterWheelMediator,
            copyMe.guiderMediator,
            copyMe.rotatorMediator,
            copyMe.flatDeviceMediator,
            copyMe.weatherDataMediator,
            copyMe.imagingMediator,
            copyMe.applicationStatusMediator,
            copyMe.nighttimeCalculator,
            copyMe.planetariumFactory,
            copyMe.imageHistoryVM,
            copyMe.deepSkyObjectSearchVM,
            copyMe.domeMediator,
            copyMe.imageSaveMediator,
            copyMe.switchMediator,
            copyMe.safetyMonitorMediator,
            copyMe.resourceDictionary,
            copyMe.applicationMediator,
            copyMe.framingAssistantVM,
            copyMe.plateSolverFactory,
            copyMe.windowServiceFactory,
            copyMe.domeFollower,
            copyMe.starDetectionSelector,
            copyMe.starAnnotatorSelector,
            copyMe.imageDataFactory,
            copyMe.meridianFlipVMFactory,
            copyMe.autoFocusVMFactory,
            copyMe.imageControlVM,
            copyMe.imageStatisticsVM,
            copyMe.domeSynchronization,
            copyMe.sequenceMediator,
            copyMe.optionsVM,
            copyMe.exposureDataFactory,
            copyMe.twilightCalculator,
            copyMe.messageBroker,
            copyMe.symbolBroker,
            copyMe.templateLinkResolver) {
            CopyMetaData(copyMe);
            Script = copyMe.Script;
            ScriptExpanded = copyMe.ScriptExpanded;
            TriggeredInstructionsExpanded = copyMe.TriggeredInstructionsExpanded;
            Timing = copyMe.Timing;
            ScriptSource = copyMe.ScriptSource;
            ScriptFilePath = copyMe.ScriptFilePath;
            ScriptPreviewExpanded = copyMe.ScriptPreviewExpanded;
            TriggerRunner = (SequentialContainer)copyMe.TriggerRunner.Clone();
        }

        public override bool AllowMultiplePerSet => true;

        public IReadOnlyList<PythonTriggerTimingOption> TimingOptions => triggerTimingOptions;

        public IList<string> Issues {
            get => issues;
            set {
                issues = value.ToList();
                RaisePropertyChanged();
            }
        }

        [ObservableProperty]
        [property: JsonProperty]
        [property: JsonConverter(typeof(StringEnumConverter))]
        private PythonTriggerTiming timing = PythonTriggerTiming.BeforeNextItem;

        [ObservableProperty]
        [property: JsonProperty]
        private string script = """
            # N.I.N.A. checks this at sequence item boundaries.
            # phase is "before" before nextItem starts, or "after" after previousItem completes.
            # Keep this predicate fast. Put long work in Triggered Instructions.
            # Set result truthy to run the triggered instructions.
            result = False
            """;

        [ObservableProperty]
        [property: JsonProperty]
        private bool scriptExpanded = true;

        [ObservableProperty]
        private string scriptPreview = string.Empty;

        [ObservableProperty]
        private string scriptPreviewStatus = PythonScriptSourceHelper.PreviewNotLoadedStatus;

        [ObservableProperty]
        private int currentExecutionLine;

        [ObservableProperty]
        [property: JsonProperty]
        private bool scriptPreviewExpanded;

        [ObservableProperty]
        [property: JsonProperty]
        private bool triggeredInstructionsExpanded = true;

        [JsonProperty]
        [JsonConverter(typeof(StringEnumConverter))]
        public PythonScriptSource ScriptSource {
            get => scriptSource;
            set {
                if (scriptSource == value) {
                    return;
                }

                scriptSource = value;
                ClearScriptPreview();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SelectScriptFileToolTip));
                Validate();
            }
        }

        [JsonProperty]
        public string ScriptFilePath {
            get => scriptFilePath;
            set {
                if (scriptFilePath == value) {
                    return;
                }

                scriptFilePath = value ?? string.Empty;
                ClearScriptPreview();
                RaisePropertyChanged();
                Validate();
            }
        }

        public IReadOnlyList<PythonScriptSourceOption> ScriptSourceOptions => PythonScriptSourceHelper.SourceOptions;

        public string SelectScriptFileToolTip => PythonScriptSourceHelper.SelectScriptFileToolTip(ScriptSource);

        public ICommand LoadScriptFromFileCommand { get; }

        public ICommand RefreshScriptPreviewCommand { get; }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext context) {
            EnsureAreaContainer();
            AttachTriggerRunnerToContext(Parent);
            Validate();
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (Timing == PythonTriggerTiming.AfterCompletedItem) {
                return false;
            }

            return EvaluateTriggerScript("before", previousItem, nextItem);
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (Timing == PythonTriggerTiming.BeforeNextItem) {
                return false;
            }

            return EvaluateTriggerScript("after", previousItem, nextItem);
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            ISequenceContainer originalParent = TriggerRunner.Parent;
            ISequenceContainer runtimeParent = ItemUtility.CreateTriggerRunnerContext(context ?? Parent);

            if (!ReferenceEquals(TriggerRunner.Parent, runtimeParent)) {
                TriggerRunner.AttachNewParent(runtimeParent);
            }

            try {
                TriggerRunner.ResetAll();
                await TriggerRunner.Run(progress, token);
            } finally {
                if (!ReferenceEquals(TriggerRunner.Parent, originalParent)) {
                    TriggerRunner.AttachNewParent(originalParent);
                }
            }
        }

        public override void AfterParentChanged() {
            EnsureAreaContainer();
            AttachTriggerRunnerToContext(Parent);
            Validate();
        }

        public override void SequenceBlockInitialize() {
            state = new Dictionary<string, object>();
        }

        public bool Validate() {
            EnsureAreaContainer();
            var i = new List<string>();
            var valid = true;

            if (ScriptSource == PythonScriptSource.Inline && string.IsNullOrWhiteSpace(Script)) {
                i.Add("Python trigger script must not be empty.");
                valid = false;
            }

            if (!PythonScriptSourceHelper.TryValidateScriptSource(ScriptSource, ScriptFilePath, out string issue)) {
                i.Add(issue);
                valid = false;
            }

            if (!TriggerRunner.GetItemsSnapshot().Any()) {
                i.Add("Python trigger needs at least one triggered instruction.");
                valid = false;
            }

            TriggerRunner.Validate();
            i.AddRange(TriggerRunner.Issues);

            Issues = i;
            return valid && i.Count == 0;
        }

        public override object Clone() {
            return new PythonScriptingTrigger(this);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(PythonScriptingTrigger)}, Timing: {Timing}, Script: {Script}";
        }

        private void SelectScriptFile() {
            var dialog = new OpenFileDialog {
                Title = ScriptSource == PythonScriptSource.File ? "Select external Python trigger script" : "Import Python trigger script",
                FileName = "",
                DefaultExt = ".py",
                Filter = "Python scripts|*.py;*.pyw|Text files|*.txt|All files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) {
                return;
            }

            if (ScriptSource == PythonScriptSource.File) {
                ScriptFilePath = dialog.FileName;
                if (!PythonScriptSourceHelper.TryValidateFilePathSyntax(ScriptFilePath, out string issue)) {
                    Notification.ShowError(issue);
                    return;
                }

                RefreshScriptPreview();
                return;
            }

            try {
                Script = PythonScriptSourceHelper.ImportScriptFile(dialog.FileName);
            } catch (Exception ex) {
                Logger.Error("Failed to load Python trigger script file", ex);
                Notification.ShowError($"Failed to load Python trigger script file: {ex.Message}");
            }
        }

        private void RefreshScriptPreview() {
            try {
                ScriptPreview = PythonScriptSourceHelper.ReadScriptFile(ScriptFilePath);
                ScriptPreviewStatus = $"Preview loaded from {PythonScriptSourceHelper.ResolveAbsoluteFilePath(ScriptFilePath)} at {DateTime.Now:G}.";
            } catch (Exception ex) {
                ScriptPreview = string.Empty;
                ScriptPreviewStatus = $"Preview failed: {ex.Message}";
                Logger.Error("Failed to refresh Python trigger script preview", ex);
                Notification.ShowError($"Failed to refresh Python trigger script preview: {ex.Message}");
            }
        }

        private bool EvaluateTriggerScript(string phase, ISequenceItem previousItem, ISequenceItem nextItem) {
            var scriptToExecute = PythonScriptSourceHelper.GetScriptExecutionSource(ScriptSource, Script, ScriptFilePath);
            using var executionLineReporter = new PythonScriptExecutionLineReporter(lineNumber => CurrentExecutionLine = lineNumber);

            var result = PythonRuntimeManager.Execute(() => {
                using var scope = Py.CreateScope();

                // Register all plugin injected services and mediators to the Python scope
                scope.Set("profileService", this.profileService.ToPython());
                scope.Set("profile", this.profileService.ActiveProfile.ToPython());
                scope.Set("camera", this.cameraMediator.ToPython());
                scope.Set("telescope", this.telescopeMediator.ToPython());
                scope.Set("focuser", this.focuserMediator.ToPython());
                scope.Set("filterWheel", this.filterWheelMediator.ToPython());
                scope.Set("guider", this.guiderMediator.ToPython());
                scope.Set("rotator", this.rotatorMediator.ToPython());
                scope.Set("flatDevice", this.flatDeviceMediator.ToPython());
                scope.Set("weatherData", this.weatherDataMediator.ToPython());
                scope.Set("imaging", this.imagingMediator.ToPython());
                scope.Set("applicationStatusMediator", this.applicationStatusMediator.ToPython());
                scope.Set("nighttimeCalculator", this.nighttimeCalculator.ToPython());
                scope.Set("planetariumFactory", this.planetariumFactory.ToPython());
                scope.Set("imageHistoryVM", this.imageHistoryVM.ToPython());
                scope.Set("deepSkyObjectSearchVM", this.deepSkyObjectSearchVM.ToPython());
                scope.Set("dome", this.domeMediator.ToPython());
                scope.Set("imageSave", this.imageSaveMediator.ToPython());
                scope.Set("switch", this.switchMediator.ToPython());
                scope.Set("safetyMonitor", this.safetyMonitorMediator.ToPython());
                scope.Set("resourceDictionary", this.resourceDictionary.ToPython());
                scope.Set("application", this.applicationMediator.ToPython());
                scope.Set("framingAssistantVM", this.framingAssistantVM.ToPython());
                scope.Set("plateSolverFactory", this.plateSolverFactory.ToPython());
                scope.Set("windowServiceFactory", this.windowServiceFactory.ToPython());
                scope.Set("domeFollower", this.domeFollower.ToPython());
                scope.Set("starDetectionSelector", this.starDetectionSelector.ToPython());
                scope.Set("starAnnotatorSelector", this.starAnnotatorSelector.ToPython());
                scope.Set("imageDataFactory", this.imageDataFactory.ToPython());
                scope.Set("meridianFlipVMFactory", this.meridianFlipVMFactory.ToPython());
                scope.Set("autoFocusVMFactory", this.autoFocusVMFactory.ToPython());
                scope.Set("imageControlVM", this.imageControlVM.ToPython());
                scope.Set("imageStatisticsVM", this.imageStatisticsVM.ToPython());
                scope.Set("domeSynchronization", this.domeSynchronization.ToPython());
                scope.Set("sequence", this.sequenceMediator.ToPython());
                scope.Set("optionsVM", this.optionsVM.ToPython());
                scope.Set("exposureDataFactory", this.exposureDataFactory.ToPython());
                scope.Set("twilightCalculator", this.twilightCalculator.ToPython());
                scope.Set("messageBroker", this.messageBroker.ToPython());
                scope.Set("symbolBroker", this.symbolBroker.ToPython());
                scope.Set("templateLinkResolver", this.templateLinkResolver.ToPython());
                PythonScriptScopeHelper.RegisterSymbolSnapshot(scope, this.symbolBroker);
                PythonScriptScopeHelper.RegisterSequenceVariableSnapshot(scope, Parent);

                // Register trigger context
                scope.Set("phase", phase.ToPython());
                scope.Set("previousItem", ToPythonOrNone(previousItem));
                scope.Set("nextItem", ToPythonOrNone(nextItem));
                scope.Set("parent", ToPythonOrNone(Parent));
                scope.Set("trigger", this.ToPython());
                scope.Set("triggerRunner", TriggerRunner.ToPython());
                scope.Set("state", state.ToPython());

                // Register basic types to call methods
                scope.Set("TimeSpan", typeof(TimeSpan).ToPython());
                scope.Set("Guid", typeof(Guid).ToPython());
                scope.Set("CaptureSequence", typeof(CaptureSequence).ToPython());
                scope.Set("Coordinates", typeof(Coordinates).ToPython());
                scope.Set("TopocentricCoordinates", typeof(TopocentricCoordinates).ToPython());
                scope.Set("SiderealShiftTrackingRate", typeof(SiderealShiftTrackingRate).ToPython());
                scope.Set("FilterInfo", typeof(FilterInfo).ToPython());
                scope.Set("PrepareImageParameters", typeof(PrepareImageParameters).ToPython());

                if (scriptToExecute.FilePath != null) {
                    scope.Set("__file__", scriptToExecute.FilePath.ToPython());
                }

                PythonScriptExecutor.Execute(scope, scriptToExecute, executionLineReporter.Report);

                if (!scope.Contains("result")) {
                    throw new SequenceEntityFailedException("Python trigger script must assign a result value.");
                }

                using var pythonResult = scope.Get("result");
                return pythonResult.IsTrue();
            });

            return result;
        }

        private void EnsureAreaContainer() {
            TriggerRunner ??= CreateAreaContainer();

            if (string.IsNullOrWhiteSpace(TriggerRunner.Name)) {
                TriggerRunner.Name = "Triggered Instructions";
            }
        }

        private void AttachTriggerRunnerToContext(ISequenceContainer parent) {
            TriggerRunner?.AttachNewParent(ItemUtility.CreateTriggerRunnerContext(parent));
        }

        private SequentialContainer CreateAreaContainer() {
            SequentialContainer container = new SequentialContainer() {
                Name = "Triggered Instructions",
                IsExpanded = true
            };

            return container.AddMetaData(resourceDictionary) as SequentialContainer ?? container;
        }

        private static object ToPythonOrNone(object value) {
            return value == null ? null : value.ToPython();
        }

        private void ClearScriptPreview() {
            ScriptPreview = string.Empty;
            ScriptPreviewStatus = PythonScriptSourceHelper.PreviewNotLoadedStatus;
        }
    }
}
