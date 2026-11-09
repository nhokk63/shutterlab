using UnityEngine;

namespace ShuttleLab.Core
{
    /// <summary>
    /// Single source of truth for all court geometry (BWF Laws of Badminton).
    /// Units are metres. Net plane is z = 0. Near side (user) is z &lt; 0,
    /// far side (AI) is z &gt; 0. X is court width, Y is up.
    /// </summary>
    public static class CourtDimensions
    {
        public const float CourtLength = 13.40f;
        public const float CourtHalfLength = 6.70f;

        public const float DoublesWidth = 6.10f;
        public const float DoublesHalfWidth = 3.05f;

        public const float SinglesWidth = 5.18f;
        public const float SinglesHalfWidth = 2.59f;

        public const float NetHeightCenter = 1.524f;
        public const float NetHeightPost = 1.55f;
        public const float PostHeight = 1.55f;

        public const float LineWidth = 0.04f;

        /// <summary>Distance from the net to the short service line.</summary>
        public const float ShortServiceDistance = 1.98f;

        /// <summary>Distance from the net to the doubles long service line.</summary>
        public const float DoublesLongServiceDistance = 5.94f;

        /// <summary>Distance from the net to the singles long service line (= back boundary).</summary>
        public const float SinglesLongServiceDistance = 6.70f;

        /// <summary>Net height at a given x (linear taper centre -&gt; posts).</summary>
        public static float NetHeightAt(float x)
        {
            float t = Mathf.Clamp01(Mathf.Abs(x) / DoublesHalfWidth);
            return Mathf.Lerp(NetHeightCenter, NetHeightPost, t);
        }

        public static bool IsInSingles(Vector3 p)
        {
            return Mathf.Abs(p.x) <= SinglesHalfWidth + LineWidth * 0.5f
                && Mathf.Abs(p.z) <= CourtHalfLength + LineWidth * 0.5f;
        }

        public static bool IsInDoubles(Vector3 p)
        {
            return Mathf.Abs(p.x) <= DoublesHalfWidth + LineWidth * 0.5f
                && Mathf.Abs(p.z) <= CourtHalfLength + LineWidth * 0.5f;
        }

        /// <summary>
        /// Is a point inside a service box? sideSign: -1 = near side (z&lt;0), +1 = far side.
        /// xSign: +1 = box with x &gt; 0, -1 = box with x &lt; 0. doubles: use doubles long line.
        /// </summary>
        public static bool IsInServiceBox(Vector3 p, int sideSign, int xSign, bool doubles)
        {
            float zMin = ShortServiceDistance;
            float zMax = doubles ? DoublesLongServiceDistance : SinglesLongServiceDistance;
            float az = Mathf.Abs(p.z);
            if (Mathf.Sign(p.z) != sideSign) return false;
            if (az < zMin || az > zMax) return false;
            if (Mathf.Sign(p.x) != xSign) return false;
            if (Mathf.Abs(p.x) > SinglesHalfWidth) return false;
            return true;
        }

        /// <summary>Clamp a position to the playable court (singles or doubles).</summary>
        public static Vector3 ClampToCourt(Vector3 p, bool doubles)
        {
            float hw = doubles ? DoublesHalfWidth : SinglesHalfWidth;
            p.x = Mathf.Clamp(p.x, -hw, hw);
            p.z = Mathf.Clamp(p.z, -CourtHalfLength, CourtHalfLength);
            return p;
        }

        /// <summary>
        /// Service-court X sign for a server standing on the given side.
        /// Even server score -&gt; server's RIGHT court; odd -&gt; LEFT court.
        /// Near server faces +z, so their right is x &lt; 0. Far server faces -z, right is x &gt; 0.
        /// </summary>
        public static int ServiceCourtXSign(int serverSideSign, bool serverScoreEven)
        {
            // right court: near side -> x<0 (-1); far side -> x>0 (+1)
            int rightSign = serverSideSign < 0 ? -1 : 1;
            return serverScoreEven ? rightSign : -rightSign;
        }

        /// <summary>Where the server stands (just behind the short service line, centre of their box).</summary>
        public static Vector3 ServerPosition(int serverSideSign, int courtXSign)
        {
            float z = serverSideSign * (ShortServiceDistance + 1.6f);
            float x = courtXSign * SinglesHalfWidth * 0.5f;
            return new Vector3(x, 0f, z);
        }

        /// <summary>Where the receiver waits (diagonal box, ~1 m behind the short service line).</summary>
        public static Vector3 ReceiverPosition(int receiverSideSign, int courtXSign)
        {
            float z = receiverSideSign * (ShortServiceDistance + 1.2f);
            float x = courtXSign * SinglesHalfWidth * 0.5f;
            return new Vector3(x, 0f, z);
        }

        /// <summary>Centre of a service box (for serve targeting).</summary>
        public static Vector3 ServiceBoxCentre(int sideSign, int xSign, bool doubles)
        {
            float zMax = doubles ? DoublesLongServiceDistance : SinglesLongServiceDistance;
            float z = sideSign * (ShortServiceDistance + zMax) * 0.5f;
            float x = xSign * SinglesHalfWidth * 0.5f;
            return new Vector3(x, 0f, z);
        }
    }
}
