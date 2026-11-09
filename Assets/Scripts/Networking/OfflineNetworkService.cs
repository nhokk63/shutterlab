// OfflineNetworkService.cs — ShuttleLab (Unity 6)
//
// Bản triển khai offline TRUNG THỰC của INetworkService (v0.1.0):
//  - Không gọi mạng thật (không có UnityWebRequest, không có socket).
//  - Mọi hàm online trả về NetworkStatus.NotAvailable kèm thông báo
//    "tính năng online chưa khả dụng" — UI hiển thị đúng thông báo đó.
//  - KHÔNG bịa dữ liệu bảng xếp hạng, KHÔNG bịa bạn bè, KHÔNG tự sinh
//    điểm giả để "cho có". Data luôn là null/mảng rỗng khi không Ok.
//
// Khi có server thật: viết OnlineNetworkService : INetworkService và
// đổi chỗ dùng Shared ở đây — không đổi interface, không đổi UI.
//
// Phụ thuộc: ShuttleLab.Networking (INetworkService). Không phụ thuộc
// module game nào.

namespace ShuttleLab.Networking
{
    /// <summary>Bản offline của INetworkService. Dùng chung qua Shared.</summary>
    public class OfflineNetworkService : INetworkService
    {
        public static OfflineNetworkService Shared { get; } = new OfflineNetworkService();

        public const string OfflineMessage =
            "Tính năng online chưa khả dụng (chế độ offline v0.1.0).";

        public bool IsOnline => false;

        public NetworkResult<LeaderboardEntry[]> GetLeaderboard(int topN)
        {
            return NetworkResult<LeaderboardEntry[]>.Fail(
                NetworkStatus.NotAvailable,
                OfflineMessage + " Bảng xếp hạng cần kết nối server.");
        }

        public NetworkResult<bool> SubmitScore(int rankingPoints)
        {
            return NetworkResult<bool>.Fail(
                NetworkStatus.NotAvailable,
                OfflineMessage + " Điểm của bạn chỉ lưu trên máy.");
        }

        public NetworkResult<string[]> GetFriends()
        {
            return NetworkResult<string[]>.Fail(
                NetworkStatus.NotAvailable,
                OfflineMessage + " Danh sách bạn bè cần kết nối server.");
        }
    }
}
