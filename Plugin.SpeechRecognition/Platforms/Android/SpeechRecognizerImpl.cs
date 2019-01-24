using System;
using System.Diagnostics;
using System.Reactive.Linq;
using Android.App;
using Android.Content;
using Android.Speech;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;


namespace Plugin.SpeechRecognition
{
    public class SpeechRecognizerImpl : AbstractSpeechRecognizer
    {
        readonly object syncLock = new object();
        readonly IPermissions permissions;


        public SpeechRecognizerImpl(IPermissions permissions = null) => this.permissions = permissions ?? CrossPermissions.Current;


        public override bool IsSupported => SpeechRecognizer.IsRecognitionAvailable(Application.Context);
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
                    return SpeechRecognizerStatus.PermissionUnknown;

                case PermissionStatus.Disabled:
                    return SpeechRecognizerStatus.Disabled;

                default:
                    return SpeechRecognizerStatus.Available;
            }
        });


        public override IObservable<string> ListenUntilPause(int silenceTimeouMilliseconds, bool playSound, int sound) => Observable.Create<string>(ob =>
        {
            var final = "";
            var listener = new SpeechRecognitionEventListener //new SpeechRecognitionListener
            {
                ReadyForSpeech = () => this.ListenSubject.OnNext(true),
                Error = ex => ob.OnError(new Exception("Failure in speech engine - " + ex)),
                PartialResults = sentence =>
                {
                    Debug.WriteLine("PartialResults BEFORE lock, sentence : " + sentence + ", finale : " + final);

                    lock (this.syncLock)
                    {
                        final = sentence;
                        Debug.WriteLine("PartialResults AFTER lock, sentence : " + sentence + ", finale : " + final);
                    }
                        
                },
                FinalResults = sentence =>
                {
                    Debug.WriteLine("FinalResults BEFORE lock, sentence : " + sentence + ", finale : " + final);
                    lock (this.syncLock)
                    {
                        final = sentence;
                        Debug.WriteLine("FinalResults AFTER lock, sentence : " + sentence + ", finale : " + final);
                        ob.OnNext(final);
                        ob.OnCompleted();
                        this.ListenSubject.OnNext(false);
                    }
                        

                },
                /*EndOfSpeech = () =>
                {
                    lock (this.syncLock)
                    {
                        Debug.WriteLine("EndOfSpeech, finale : " + final);
                        ob.OnNext(final);
                        ob.OnCompleted();
                        this.ListenSubject.OnNext(false);
                    }
                }**/
            };
            var speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(Application.Context);

            //speechRecognizer.SetRecognitionListener(listener);
            SetEvents(speechRecognizer, listener);

            speechRecognizer.StartListening(this.CreateSpeechIntent(true, silenceTimeouMilliseconds));

            return () =>
            {
                this.ListenSubject.OnNext(false);
                speechRecognizer.StopListening();
                speechRecognizer.Destroy();
            };
        });

        private void SetEvents(SpeechRecognizer speechRecognizer, SpeechRecognitionEventListener listener) {
            speechRecognizer.ReadyForSpeech += listener.OnReadyForSpeech;
            speechRecognizer.Results += listener.OnResults;
            speechRecognizer.PartialResults += listener.OnPartialResults;
            speechRecognizer.BeginningOfSpeech += listener.OnBeginningOfSpeech;
            speechRecognizer.EndOfSpeech += listener.OnEndOfSpeech;
            speechRecognizer.BufferReceived += listener.OnBufferReceived;
            speechRecognizer.Error += listener.OnError;
            speechRecognizer.Event += listener.OnEvent;
            speechRecognizer.RmsChanged += listener.OnRmsChanged;
        }

        public override IObservable<string> ContinuousDictation() => Observable.Create<string>(ob =>
        {
            var stop = false;
            var currentIndex = 0;
            var speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(Application.Context);
            var listener = new SpeechRecognitionEventListener();//new SpeechRecognitionListener();

            listener.ReadyForSpeech = () => this.ListenSubject.OnNext(true);
            listener.PartialResults = sentence =>
            {
                lock (this.syncLock)
                {
                    sentence = sentence.Trim();
                    if (currentIndex > sentence.Length)
                        currentIndex = 0;

                    var newPart = sentence.Substring(currentIndex);
                    currentIndex = sentence.Length;
                    ob.OnNext(newPart);
                }
            };

            listener.EndOfSpeech = () =>
            {
                lock (this.syncLock)
                {
                    currentIndex = 0;
                    speechRecognizer.Destroy();
                    speechRecognizer = null;

                    speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(Application.Context);
                    //speechRecognizer.SetRecognitionListener(listener);
                    SetEvents(speechRecognizer, listener);
                    speechRecognizer.StartListening(this.CreateSpeechIntent(true));
                }
            };
            listener.Error = ex =>
            {
                switch (ex)
                {
                    case SpeechRecognizerError.Client:
                    case SpeechRecognizerError.RecognizerBusy:
                    case SpeechRecognizerError.SpeechTimeout:
                        lock (this.syncLock)
                        {
                            if (stop)
                                return;

                            speechRecognizer.Destroy();
                            speechRecognizer = null;

                            speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(Application.Context);
                            //speechRecognizer.SetRecognitionListener(listener);
                            SetEvents(speechRecognizer, listener);
                            speechRecognizer.StartListening(this.CreateSpeechIntent(true));
                        }
                        break;

                    default:
                        ob.OnError(new Exception($"Could not start speech recognizer - ERROR: {ex}"));
                        break;
                }
            };
            //speechRecognizer.SetRecognitionListener(listener);
            SetEvents(speechRecognizer, listener);
            speechRecognizer.StartListening(this.CreateSpeechIntent(true));

            return () =>
            {
                stop = true;
                speechRecognizer?.StopListening();
                speechRecognizer?.Destroy();
                this.ListenSubject.OnNext(false);
            };
        });

        protected virtual Intent CreateSpeechIntent(bool partialResults, int silenceTimeouMilliseconds = 0)
        {
            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, Java.Util.Locale.Default);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraCallingPackage, Application.Context.PackageName);
            //intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
            if (silenceTimeouMilliseconds > 0)
            {
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, silenceTimeouMilliseconds);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, silenceTimeouMilliseconds);
            }
            //intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 15000);
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, partialResults);

            return intent;
        }
    }
}


