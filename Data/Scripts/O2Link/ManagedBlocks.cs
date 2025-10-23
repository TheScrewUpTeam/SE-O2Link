namespace TSUT.O2Link
{
    public interface IManagedBlock
    {
        void Enable();
        void Disable();
        bool IsWorking { get; }
        void Dismiss();
    }
}