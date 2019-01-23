using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AVFoundation;
using Foundation;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Speech;
using UIKit;


namespace Plugin.SpeechRecognition
{
    public class SpeechRecognizerImpl : AbstractSpeechRecognizer
    {
        readonly IPermissions permissions;


        public SpeechRecognizerImpl(IPermissions permissions = null) => this.permissions = permissions ?? CrossPermissions.Current;


        public override bool IsSupported => UIDevice.CurrentDevice.CheckSystemVersion(10, 0);
        public override IObservable<string> ListenUntilPause(int silenceTimeouMilliseconds) => this.Listen(silenceTimeouMilliseconds, true);
        public override IObservable<string> ContinuousDictation() => this.Listen(0, false);


        public override IObservable<SpeechRecognizerStatus> RequestPermission() => Observable.FromAsync(async ct =>
        {
            if (!this.IsSupported)
                return SpeechRecognizerStatus.NotSupported;

            var status = await this.permissions.RequestPermissionsAsync(Permission.Speech);
            switch (status[Permission.Speech])
            {
                case PermissionStatus.Restricted:
                case PermissionStatus.Denied:
                    return SpeechRecognizerStatus.PermissionDenied;

                case PermissionStatus.Unknown:
                    return SpeechRecognizerStatus.NotSupported;

                case PermissionStatus.Disabled:
                    return SpeechRecognizerStatus.Disabled;

                default:
                    return SpeechRecognizerStatus.Available;
            }
        });

        private int SilenceTimeouMilliseconds = 0;
        private CancellationTokenSource TimerTokenSource = null;

        private void StartTimer() {
            if (TimerTokenSource != null) {
                TimerTokenSource.Cancel();
                TimerTokenSource.Dispose();
            }
            if (SilenceTimeouMilliseconds <= 0) return;

            TimerTokenSource = new CancellationTokenSource();
            var token = TimerTokenSource.Token;
            Task.Run(async () => {
                await Task.Delay(SilenceTimeouMilliseconds);
                if (!token.IsCancellationRequested) StopRecording();
            }, token);
        }

        private void StopTimer() {
            if (TimerTokenSource != null)
            {
                TimerTokenSource.Cancel();
                TimerTokenSource.Dispose();
                TimerTokenSource = null;
            }
            
        }

        private SFSpeechRecognizer SpeechRecognizer;
        private AVAudioEngine AudioEngine;
        private SFSpeechAudioBufferRecognitionRequest SpeechRequest;
        protected virtual IObservable<string> Listen(int silenceTimeouMilliseconds, bool completeOnEndOfSpeech) => Observable.Create<string>(ob =>
        {
            SilenceTimeouMilliseconds = silenceTimeouMilliseconds;
            SpeechRecognizer = new SFSpeechRecognizer();
            if (!SpeechRecognizer.Available)
                throw new ArgumentException("Speech recognizer is not available");

            SpeechRequest = new SFSpeechAudioBufferRecognitionRequest();
            AudioEngine = new AVAudioEngine();
            var format = AudioEngine.InputNode.GetBusOutputFormat(0);

            if (!completeOnEndOfSpeech) SpeechRequest.TaskHint = SFSpeechRecognitionTaskHint.Dictation;

            AudioEngine.InputNode.InstallTapOnBus(
                0,
                1024,
                format,
                (buffer, when) => SpeechRequest.Append(buffer)
            );
            AudioEngine.Prepare();
            AudioEngine.StartAndReturnError(out var error);

            if (error != null)
                throw new ArgumentException("Error starting audio engine - " + error.LocalizedDescription);

            this.ListenSubject.OnNext(true);

            if (completeOnEndOfSpeech) StartTimer();

            var currentIndex = 0;
            var cancel = false;
            var task = SpeechRecognizer.GetRecognitionTask(SpeechRequest, (result, err) =>
            {
                if (cancel)
                    return;

                if (err != null)
                {
                    ob.OnError(new Exception(err.LocalizedDescription));
                }
                else
                {
                    if (result.Final && completeOnEndOfSpeech)
                    {
                        currentIndex = 0;
                        ob.OnNext(result.BestTranscription.FormattedString);
                        ob.OnCompleted();
                    }
                    else
                    {
                        if (completeOnEndOfSpeech) StartTimer();

                        for (var i = currentIndex; i < result.BestTranscription.Segments.Length; i++)
                        {
                            var s = result.BestTranscription.Segments[i].Substring;
                            currentIndex++;
                            ob.OnNext(s);
                        }
                    }
                }
            });

            if (completeOnEndOfSpeech) StopTimer();

            return () =>
            {
                cancel = true;
                task?.Cancel();
                task?.Dispose();
                StopRecording();
                this.ListenSubject.OnNext(false);
            };
        });

        public void StopRecording() {

            AudioEngine?.Stop();
            AudioEngine?.InputNode?.RemoveTapOnBus(0);
            AudioEngine?.Dispose();
            AudioEngine = null;
            SpeechRequest?.EndAudio();
            SpeechRequest?.Dispose();
            SpeechRequest = null;
            SpeechRecognizer?.Dispose();
            SpeechRecognizer = null;
        }
    }
}

//      protected virtual IObservable<string> Listen(bool completeOnEndOfSpeech) => Observable.Create<string>(ob =>
//      {
//    var speechRecognizer = new SFSpeechRecognizer();
//          var path = NSBundle.MainBundle.GetUrlForResource("plugin", "m4a");
//          var speechRequest = new SFSpeechUrlRecognitionRequest(path);

//  this.ListenSubject.OnNext(true);
//          var task = speechRecognizer.GetRecognitionTask(speechRequest, (result, err) =>
//          {
//           if (err != null)
//           {
//               ob.OnError(new Exception(err.LocalizedDescription));
//           }
//           else if (result.Final)
//           {
//               ob.OnNext(result.BestTranscription.FormattedString);
//               if (completeOnEndOfSpeech)
//                   ob.OnCompleted();
//           }
//  });

//          return () =>
//          {
//        task?.Cancel();
//        task?.Dispose();
//        speechRequest.Dispose();
//        speechRecognizer.Dispose();
//        this.ListenSubject.OnNext(false);
//  };
//});