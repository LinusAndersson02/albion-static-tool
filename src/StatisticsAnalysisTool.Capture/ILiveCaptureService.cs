namespace StatisticsAnalysisTool.Capture;

public interface ILiveCaptureService
{
    bool IsRunning { get; }

    LiveCaptureStatus GetStatus();

    LiveCaptureStatus Start(LiveCaptureOptions options);

    LiveCaptureStatus Stop();
}
