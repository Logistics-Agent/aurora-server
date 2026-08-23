using Notification.Grpc;

namespace Notification.Tests;

public sealed class NotificationContractTests
{
    [Fact]
    public void ListNotificationsRequestPreservesPaginationAndUnreadFilter()
    {
        var request = new ListNotificationsRequest
        {
            Page = 2,
            PageSize = 25,
            UnreadOnly = true
        };

        Assert.Equal(2, request.Page);
        Assert.Equal(25, request.PageSize);
        Assert.True(request.UnreadOnly);
    }
}
