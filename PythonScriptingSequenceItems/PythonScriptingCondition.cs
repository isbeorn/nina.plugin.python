using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using Newtonsoft.Json;
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
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using Python.Runtime;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Input;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    [ExportMetadata("Name", "Python Condition")]
    [ExportMetadata("Description", "Evaluates a Python script and uses its result variable as the sequencer condition.")]
    [ExportMetadata("Icon", "Plugin_Test_SVG")]
    [ExportMetadata("Category", "Python Scripting")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class PythonScriptingCondition : SequenceCondition {
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

        [ImportingConstructor]
        public PythonScriptingCondition(
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
            LoadScriptFromFileCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(LoadScriptFromFile);
        }

        private PythonScriptingCondition(PythonScriptingCondition copyMe) : this(
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
        }

        public override bool AllowMultiplePerSet => true;

        [ObservableProperty]
        [property: JsonProperty]
        private string script = """
            # In sequential containers, N.I.N.A. checks this while selecting each next item,
            # including after an item completes, and after the block finishes to decide repeat.
            # Keep it fast. Truthy result keeps the loop running; falsey stops it.
            result = True
            """;

        [ObservableProperty]
        [property: JsonProperty]
        private bool scriptExpanded = true;

        public ICommand LoadScriptFromFileCommand { get; }

        private void LoadScriptFromFile() {
            var dialog = new OpenFileDialog {
                Title = "Load Python condition script",
                FileName = "",
                DefaultExt = ".py",
                Filter = "Python scripts|*.py;*.pyw|Text files|*.txt|All files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) {
                return;
            }

            try {
                Script = File.ReadAllText(dialog.FileName);
            } catch (Exception ex) {
                Logger.Error("Failed to load Python condition script file", ex);
                Notification.ShowError($"Failed to load Python condition script file: {ex.Message}");
            }
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
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

                // Register condition context
                scope.Set("previousItem", ToPythonOrNone(previousItem));
                scope.Set("nextItem", ToPythonOrNone(nextItem));
                scope.Set("parent", ToPythonOrNone(Parent));
                scope.Set("condition", this.ToPython());

                // Register basic types to call methods
                scope.Set("TimeSpan", typeof(TimeSpan).ToPython());
                scope.Set("Guid", typeof(Guid).ToPython());
                scope.Set("CaptureSequence", typeof(CaptureSequence).ToPython());
                scope.Set("Coordinates", typeof(Coordinates).ToPython());
                scope.Set("TopocentricCoordinates", typeof(TopocentricCoordinates).ToPython());
                scope.Set("SiderealShiftTrackingRate", typeof(SiderealShiftTrackingRate).ToPython());
                scope.Set("FilterInfo", typeof(FilterInfo).ToPython());
                scope.Set("PrepareImageParameters", typeof(PrepareImageParameters).ToPython());

                scope.Exec(Script);

                if (!scope.Contains("result")) {
                    throw new SequenceEntityFailedException("Python condition script must assign a result value.");
                }

                using var pythonResult = scope.Get("result");
                return pythonResult.IsTrue();
            });

            return result;
        }

        public override object Clone() {
            return new PythonScriptingCondition(this);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(PythonScriptingCondition)}, Script: {Script}";
        }

        private static object ToPythonOrNone(object value) {
            return value == null ? null : value.ToPython();
        }
    }
}
