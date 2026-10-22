namespace TwinInspector
{
    /// <summary>Six-card onboarding state. Only explicit Skip or Done completes onboarding.</summary>
    public sealed class GuideFlow
    {
        public const int Count=6;
        public int Index {get;private set;}
        public bool Last=>Index==Count-1;
        public void Go(int index)=>Index=System.Math.Max(0,System.Math.Min(Count-1,index));
        public void Next()=>Go(Index+1);
        public void Back()=>Go(Index-1);
        public void Reset()=>Go(0);
        public void Skip()=>InteractionPreferences.GuideSeen=true;
        public void Done(){if(Last)InteractionPreferences.GuideSeen=true;}
    }
}
