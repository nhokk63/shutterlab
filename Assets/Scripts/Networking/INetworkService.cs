// INetworkService.cs — ShuttleLab (Unity 6)
//
// Interface cho tính năng online (bảng xếp hạng, bạn bè, gửi điểm).
// Mọi hàm trả về NetworkResult<T> với trạng thái rõ ràng — caller PHẢI
// kiểm tra Status trước khi dùng Data. Không có hàm nào được phép trả về
// dữ liệu giả khi offline.
//
// Bản triển khai hiện tại: OfflineNetworkService (không gọi mạng thật).
// Khi có server thật, viết thêm OnlineNetworkService : INetworkService
// mà không đổi interface hay UI.
//
// Phụ thuộc: chỉ System. Không phụ thuộc module game nào.

using System;

namespace ShuttleLab.Networking
{
    /// <summary>Trạng thái của một thao tác mạng.</summary>
    public enum NetworkStatus
    {
        Ok,             // thành công, Data dùng được
        Offline,        // không có mạng / đang ở chế độ offline
        NotAvailable,   // tính năng chưa khả dụng ở bản này
        Error,          // lỗi khác (server, dữ liệu...)
    }

    /// <summary>Kết quả một thao tác mạng. Data chỉ có nghĩa khi Status == Ok.</summary>
    public struct NetworkResult<T>
    {
        public readonly NetworkStatus Status;
        public readonly T Data;
        public readonly string Message;

        public bool IsOk => Status == NetworkStatus.Ok;

        public NetworkResult(NetworkStatus status, T data, string message)
        {
            Status = status;
            Data = data;
            Message = message ?? "";
        }

        public static NetworkResult<T> Ok(T data) =>
            new NetworkResult<T>(NetworkStatus.Ok, data, "");

        public static NetworkResult<T> Fail(NetworkStatus status, string message) =>
            new NetworkResult<T>(status, default(T), message);
    }

    /// <summary>Một dòng bảng xếp hạng (chỉ có nghĩa khi lấy từ server thật).</summary>
    [Serializable]
    public struct LeaderboardEntry
    {
        public int Rank;
        public string PlayerName;
        public int Points;
    }

    /// <summary>
    /// Tính năng online. Bản offline trả về NotAvailable/Offline kèm thông
    /// báo trung thực — KHÔNG bịa bảng xếp hạng, KHÔNG bịa bạn bè.
    /// </summary>
    public interface INetworkService
    {
        bool IsOnline { get; }

        /// <summary>Bảng xếp hạng toàn server (top N).</summary>
        NetworkResult<LeaderboardEntry[]> GetLeaderboard(int topN);

        /// <summary>Gửi điểm sự nghiệp của user lên server.</summary>
        NetworkResult<bool> SubmitScore(int rankingPoints);

        /// <summary>Danh sách bạn bè (tên hiển thị).</summary>
        NetworkResult<string[]> GetFriends();
    }
}
