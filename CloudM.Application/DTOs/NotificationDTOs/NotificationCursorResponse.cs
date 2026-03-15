namespace CloudM.Application.DTOs.NotificationDTOs
{
    public class NotificationCursorResponse
    {
        public Guid AccountId { get; set; }
        public List<NotificationItemResponse> Items { get; set; } = new();
        public int Count { get; set; }
        public int NotificationUnreadCount { get; set; }
        public int FollowRequestUnreadCount { get; set; }
        public int PendingFollowRequestCount { get; set; }
        public int FollowRequestCount { get; set; }
        public DateTime? LastNotificationsSeenAt { get; set; }
        public DateTime? LastFollowRequestsSeenAt { get; set; }
        public NotificationNextCursorResponse? NextCursor { get; set; }
    }

    public class NotificationNextCursorResponse
    {
        public DateTime LastEventAt { get; set; }
        public Guid NotificationId { get; set; }
    }
}
