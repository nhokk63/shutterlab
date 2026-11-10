using UnityEngine;

namespace ShuttleLab.Systems
{
    /// <summary>
    /// Carrier sống qua đổi scene: giữ TournamentManager + CareerManager
    /// của tuần giải đang chơi, để HUDManager (scene Match) ghi kết quả
    /// trận giải vào đúng bracket sau khi trận kết thúc. CareerUI bị unload
    /// khi vào trận nên không thể giữ trực tiếp.
    /// </summary>
    public class TournamentSession : MonoBehaviour
    {
        public static TournamentSession Instance { get; private set; }

        public TournamentManager Tournament;
        public CareerManager Career;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
