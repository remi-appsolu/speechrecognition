using System;
using System.Linq;
using Android.OS;
using Android.Speech;
using Debug = System.Diagnostics.Debug;


namespace Plugin.SpeechRecognition
{
    public class SpeechRecognitionEventListener //: Java.Lang.Object, IRecognitionListener
    {
        public Action StartOfSpeech { get; set; }
        public Action EndOfSpeech { get; set; }
        public Action ReadyForSpeech { get; set; }
        public Action<SpeechRecognizerError> Error { get; set; }
        public Action<string> FinalResults { get; set; }
        public Action<string> PartialResults { get; set; }
        public Action<float> RmsChanged { get; set; }

        public void OnPartialResults(object sender, PartialResultsEventArgs e)
        {
            Debug.WriteLine("OnPartialResults Remi");
            this.SendResults(e?.PartialResults, this.PartialResults);
        }

        public void OnResults(object sender, ResultsEventArgs e)
        {
            Debug.WriteLine("Speech Results");
            this.SendResults(e.Results, this.FinalResults);
        }

        public void OnReadyForSpeech(object sender, ReadyForSpeechEventArgs e)
        {
            Debug.WriteLine("Ready for Speech");
            this.ReadyForSpeech?.Invoke();
        }

        public void OnEndOfSpeech(object sender, EventArgs e)
        {
            Debug.WriteLine("End of Speech");
            this.EndOfSpeech?.Invoke();
        }

        public void OnBeginningOfSpeech(object sender, EventArgs e)
        {
            Debug.WriteLine("Ready for Speech");
            this.ReadyForSpeech?.Invoke();
        }


        public void OnError(object sender, ErrorEventArgs e)
        {
            Debug.WriteLine("Error: " + e?.Error);
            if (e != null) this.Error?.Invoke(e.Error);
        }

        public void OnEvent(object sender, EventEventArgs e)
        {
            Debug.WriteLine("OnEvent: " + e?.EventType);
        }

        public void OnRmsChanged(object sender, RmsChangedEventArgs e)
        {
            Debug.WriteLine("RMS Changed: " + e?.RmsdB);
            if (e != null) this.RmsChanged?.Invoke(e.RmsdB);
        }

        public void OnBufferReceived(object sender, BufferReceivedEventArgs e)
        {
            Debug.WriteLine("Buffer Received");
        }

        void SendResults(Bundle bundle, Action<string> action)
        {
            var matches = bundle.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            if (matches == null || matches.Count == 0)
            {
                Debug.WriteLine("Matches value is null in bundle");
                return;
            }

            var j = 0;
            

            if (Build.VERSION.SdkInt >= BuildVersionCodes.IceCreamSandwich && matches.Count > 1)
            {
                var scores = bundle.GetFloatArray(SpeechRecognizer.ConfidenceScores);

                foreach (var match in matches)
                {
                    Debug.WriteLine("match : " + j + " : " + match + ", score : " + scores[j]);
                    j++;
                }


                var best = 0;
                for (var i = 0; i < scores.Length; i++)
                {
                    if (scores[best] < scores[i])
                        best = i;
                }
                var winner = matches[best];
                action?.Invoke(winner);
            }
            else
            {
                Debug.WriteLine("match one : " + matches.First());
                action?.Invoke(matches.First());
            }
        }

    }
}