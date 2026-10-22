using NUnit.Framework;

namespace TwinInspector.Tests
{
    public class GuideTests
    {
        [Test] public void SixCardsClampAndOnlySkipOrDoneMarksSeen()
        {
            bool saved=InteractionPreferences.GuideSeen;
            try
            {
                InteractionPreferences.GuideSeen=false;var guide=new GuideFlow();Assert.That(GuideFlow.Count,Is.EqualTo(6));guide.Back();Assert.That(guide.Index,Is.Zero);guide.Done();Assert.That(InteractionPreferences.GuideSeen,Is.False);
                for(int i=0;i<8;i++)guide.Next();Assert.That(guide.Index,Is.EqualTo(5));Assert.That(InteractionPreferences.GuideSeen,Is.False);guide.Done();Assert.That(InteractionPreferences.GuideSeen,Is.True);
                InteractionPreferences.GuideSeen=false;guide.Reset();guide.Next();Assert.That(InteractionPreferences.GuideSeen,Is.False);guide.Skip();Assert.That(InteractionPreferences.GuideSeen,Is.True);
            }
            finally{InteractionPreferences.GuideSeen=saved;}
        }
    }
}
