namespace WingSinger.UI {
    public enum UIScreenId {
        HomeDashboard = 1, HomeClean, LevelSelect, ReadyForTakeoff, FlightPaused,
        FlightComplete, Settings, ControllerConnection, FlightLog,
        PracticeInhale, PracticeExhale, PracticeFollow, PracticeComplete, HardwareSettings, Tutorial
    }
    public enum UIRouteAction {
        Navigate, Back, SelectLevel, StartFlight, TryRhythm, Pause, Resume,
        Restart, NextLevel, CheckConnection, AdvancePractice, ViewRecord, ContinueSelection
    }
}
