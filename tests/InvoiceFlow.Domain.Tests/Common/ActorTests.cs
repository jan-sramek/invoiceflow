using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Domain.Tests.Common;

public class ActorTests
{
    [Fact]
    public void User_KeepsSubjectIdAndDisplayName()
    {
        var actor = Actor.User("3f2a-subject", "Clara Clerk");

        actor.Type.ShouldBe(ActorType.User);
        actor.Id.ShouldBe("3f2a-subject");
        actor.Name.ShouldBe("Clara Clerk");
    }

    [Fact]
    public void System_UsesNameAsId()
    {
        var actor = Actor.System("invoiceflow-worker");

        actor.Type.ShouldBe(ActorType.System);
        actor.Id.ShouldBe("invoiceflow-worker");
    }

    [Theory]
    [InlineData("", "Clara")]
    [InlineData(" ", "Clara")]
    [InlineData("subject", "")]
    public void User_BlankIdOrName_Throws(string subjectId, string displayName)
    {
        Should.Throw<ArgumentException>(() => Actor.User(subjectId, displayName));
    }

    [Fact]
    public void IsSameAs_SameUserWithNewDisplayName_IsTrue()
    {
        Actor.User("subject", "Clara Clerk").IsSameAs(Actor.User("subject", "Clara Novak")).ShouldBeTrue();
    }

    [Fact]
    public void IsSameAs_UserAndSystemWithSameId_IsFalse()
    {
        Actor.User("invoiceflow-worker", "Worker").IsSameAs(Actor.System("invoiceflow-worker")).ShouldBeFalse();
    }
}
