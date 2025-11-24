using System;
using FofX.Stateful;

namespace PlerionClient.Client
{
    public enum CaptureType
    {
        ARFoundation,
        Zed
    }

    public enum CaptureStatus
    {
        Idle,
        Starting,
        Capturing,
        Stopping
    }

    public enum AppMode
    {
        Capture,
        Validation
    }

    public enum LocalizationSessionStatus
    {
        Inactive,
        Starting,
        Active,
        Stopping,
        Error
    }

    public class AppState : ObservableObject
    {
        public ObservablePrimitive<string> plerionAPIBaseUrl { get; private set; }
        public ObservablePrimitive<string> plerionKeycloakUrl { get; private set; }
        public ObservablePrimitive<string> username { get; private set; }
        public ObservablePrimitive<string> password { get; private set; }
        public ObservablePrimitive<bool> loginRequested { get; private set; }
        public ObservablePrimitive<bool> vpsAuthComplete { get; private set; }
        public ObservablePrimitive<bool> apiAuthComplete { get; private set; }
        public ObservablePrimitive<bool> loggedIn { get; private set; }

        public ObservablePrimitive<AppMode> mode { get; private set; }

        public ObservablePrimitive<CaptureType> captureMode { get; private set; }
        public ObservablePrimitive<CaptureStatus> captureStatus { get; private set; }
        public ObservableDictionary<Guid, CaptureState> captures { get; private set; }

        public ObservablePrimitive<bool> localizing { get; private set; }
        public ObservablePrimitive<LocalizationSessionStatus> localizationSessionStatus { get; private set; }
        public ObservablePrimitive<Guid> mapForLocalization { get; private set; }

        protected override void PostInitializeInternal()
        {
            loggedIn.RegisterDerived(
                _ => loggedIn.value = vpsAuthComplete.value && apiAuthComplete.value,
                ObservationScope.Self,
                vpsAuthComplete,
                apiAuthComplete
            );
        }
    }

    public enum CaptureUploadStatus
    {
        NotUploaded,
        UploadRequested,
        Initializing,
        Uploading,
        ReconstructionNotStarted,
        ReconstructRequested,
        Reconstructing,
        Uploaded,
        CreateMapRequested,
        MapCreated,
        Failed
    }

    public class CaptureState : ObservableObject, IKeyedObservableNode<Guid>
    {
        public Guid id { get; private set; }

        public ObservablePrimitive<string> name { get; private set; }
        public ObservablePrimitive<CaptureType> type { get; private set; }
        public ObservablePrimitive<DateTime> createdAt { get; private set; }
        public ObservablePrimitive<CaptureUploadStatus> status { get; private set; }
        public ObservablePrimitive<float> statusPercentage { get; private set; }
        public ObservablePrimitive<Guid> reconstructionId { get; private set; }
        public ObservablePrimitive<Guid> localizationMapId { get; private set; }
        public ObservablePrimitive<bool> hasLocalFiles { get; private set; }

        void IKeyedObservableNode<Guid>.AssignKey(Guid key)
            => id = key;
    }
}