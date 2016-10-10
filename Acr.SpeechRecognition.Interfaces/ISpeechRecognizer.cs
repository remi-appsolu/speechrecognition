﻿using System;
using System.Threading.Tasks;


namespace Acr.SpeechRecognition
{
    public interface ISpeechRecognizer
    {
        bool IsSupported { get; }
        Task<bool> RequestPermission();
        IObservable<string> Listen(bool completeOnEndOfSpeech = false);
        //IObservable<string> Listen(bool completeOnEndOfSpeech = false, CultureInfo culture = null);
        //IList<CultureInfo> AvailableCultures { get; }
    }
}
