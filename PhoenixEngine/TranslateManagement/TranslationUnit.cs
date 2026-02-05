using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PhoenixEngine.DelegateManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.TranslateManage.TransCore;

namespace PhoenixEngine.TranslateManagement
{
    public class TranslationUnit
    {
        public int FileUniqueKey = 0;
        public int WorkEnd = 0;
        public Thread CurrentTrd;
        public double Score = 100;
        public string Key = "";
        public string Type = "";
        public string SourceText = "";
        public string TransText = "";
        public bool IsDuplicateSource = false;
        public bool Transing = false;
        public bool Leader = false;
        public bool Translated = false;
        public double TempSim = 0;
        public int MaxTry = 10;
        public string AIParam = "";

        private CancellationTokenSource TransThreadToken;

        public bool CanTrans(int State)
        {
            if (DelegateHelper.SetTranslationUnitCallBack != null)
            {
                return DelegateHelper.SetTranslationUnitCallBack(this, State);
            }

            return true;
        }

        public void StartWork(BatchTranslationCore Source)
        {
            if (!CanTrans(0))
            {
                this.WorkEnd = 2;
                return;
            }

            if (this.TransText.Trim().Length > 0)
            {
                this.WorkEnd = 2;
                return;
            }

            if (this.IsDuplicateSource)
            {
                lock (Source.SameItemsLocker)
                {
                    if (!Source.SameItems.ContainsKey(this.SourceText))
                    {
                        Source.SameItems.Add(this.SourceText, string.Empty);
                    }
                    else
                    {
                        this.Transing = false;
                        this.WorkEnd = 2;
                        CurrentTrd = null;

                        return;
                    }
                }
            }
            WorkEnd = 1;
            this.Transing = true;
            CurrentTrd = new Thread(() =>
            {
                TransThreadToken = new CancellationTokenSource();
                var Token = TransThreadToken.Token;
                try
                {
                NextGet:

                    Token.ThrowIfCancellationRequested();

                    if (this.SourceText.Trim().Length > 0)
                    {
                        bool CanSleep = true;

                        if (!CanTrans(1))
                        {
                            this.Transing = false;
                            this.WorkEnd = 2;
                            CurrentTrd = null;

                            return;
                        }

                        var GetResult = Translator.QuickTrans(this, ref CanSleep);
                        if (GetResult.Trim().Length > 0)
                        {
                            TransText = GetResult.Trim();

                            if (!CanTrans(2))
                            {
                                EngineSelect.AIMemory.RemoveTranslation(this.From, this.To, Translator.FormatStr(this.SourceText), TransText);

                                this.TransText = string.Empty;
                                this.Transing = false;
                                this.WorkEnd = 0;

                                CurrentTrd = null;
                                return;
                            }

                            lock (Translator.TransDataLocker)
                            {
                                if (Translator.TransData.ContainsKey(this.Key))
                                {
                                    Translator.TransData[this.Key] = GetResult;
                                }
                                else
                                {
                                    Translator.TransData.Add(this.Key, GetResult);
                                }
                            }

                            if (this.IsDuplicateSource)
                            {
                                lock (Source.SameItemsLocker)
                                {
                                    if (Source.SameItems.ContainsKey(this.SourceText))
                                    {
                                        Source.SameItems[this.SourceText] = GetResult;
                                    }
                                }
                            }

                            WorkEnd = 2;

                            this.Translated = true;

                            Source.AddTranslated(this);

                            Token.ThrowIfCancellationRequested();
                        }
                        else
                        {
                            if (this.MaxTry > 0)
                            {
                                Thread.Sleep(500);
                                this.MaxTry--;

                                goto NextGet;
                            }
                            else
                            {
                                WorkEnd = 2;
                            }
                        }
                    }
                    else
                    {
                        WorkEnd = 2;
                    }
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        this.Transing = false;
                        this.CurrentTrd = null;
                    }
                    catch { }
                }
                this.Transing = false;
                this.CurrentTrd = null;
            });
            CurrentTrd.Start();
        }

        public void CancelWorkThread()
        {
            WorkEnd = 2;
            TransThreadToken?.Cancel();
        }

        public TranslationUnit(int FileUniqueKey, string Key, string Type, string SourceText, string TransText, string AIParam, Languages From, Languages To, double Score)
        {
            this.FileUniqueKey = FileUniqueKey;
            this.Key = Key;
            this.Type = Type;
            this.SourceText = SourceText;
            this.TransText = TransText;
            this.AIParam = AIParam;
            this.From = From;
            this.To = To;
            this.Score = Score;
        }
    }

}
