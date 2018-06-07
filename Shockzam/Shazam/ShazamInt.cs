using System;
using System.Windows.Forms;

namespace Shockzam
{
    internal static class ShazamInt
    {
        public static void ShazamStateChanged(ShazamRecognitionState State, ShazamResponse response)
        {
          
            switch (State)
            {
                case ShazamRecognitionState.Sending:
                    MainForm.SetStatus("Sending..");
                    break;
                case ShazamRecognitionState.Matching:
                    MainForm.SetStatus("Matching..");

                    break;
                case ShazamRecognitionState.Done:


                    if (response.Tag != null)
                    {
                        if (response.Tag.Track != null)
                        {
                            MainForm.notifyIcon.BalloonTipText =
                                $"{response.Tag.Track.Title} - {response.Tag.Track.Artist}";
                            MainForm.notifyIcon.BalloonTipTitle =
                                "Found!";
                            MainForm.notifyIcon.ShowBalloonTip(2000);
                            MainForm.SetText(response.Tag.Track.Title + " - " +
                                             response.Tag.Track.Artist);

                        }
                        else
                        {
                            MainForm.notifyIcon.BalloonTipText = "Not Found!";
                            MainForm.notifyIcon.BalloonTipTitle =
                                "";
                            MainForm.notifyIcon.ShowBalloonTip(2000);
                        }
                    }
                    else
                    {
                        MainForm.notifyIcon.BalloonTipText = "Not Found!";
                        MainForm.notifyIcon.BalloonTipTitle =
                            "";
                        MainForm.notifyIcon.ShowBalloonTip(2000);
                    }
                    MainForm.SetStatus("Ready");
                    MainForm.SetBool(true);
                    break;
                case ShazamRecognitionState.Failed:
                  
                    if (!string.IsNullOrEmpty(response.Exception.Message))
                        throw new Exception("Failed! Message: " + response.Exception.Message);


                    break;
            }
        }
    }
}