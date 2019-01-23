using System;


namespace Plugin.SpeechRecognition
{
    public enum SpeechRecognizerStatus
    {
        NotSupported = 0,
        PermissionDenied = 1,
        Disabled = 2,
        Available = 3,
        PermissionUnknown = 4,
    }
}
