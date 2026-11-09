// AudioManager.cs — ShuttleLab (Unity 6)
//
// Âm thanh TỔNG HỢP BẰNG CODE (không asset ngoài, theo ARCHITECTURE.md):
// mọi clip được sinh bằng AudioClip.Create + SetData từ sóng sine /
// noise + envelope suy giảm. Không có file .wav/.mp3 nào trong project.
//
// QUAN TRỌNG — TRUNG THỰC VỀ NHẠC:
//  - v0.1.0 CHỈ CÓ hiệu ứng (SFX): tiếng vợt theo loại cú đánh, tiếng
//    cầu chạm sân, còi trọng tài, click UI, chuông ghi điểm.
//  - KHÔNG CÓ nhạc nền. MusicVolume vẫn được lưu ở GameStateManager và
//    sẽ điều khiển nhạc khi có track nhạc ở bản sau — hiện tại slider
//    nhạc chưa phát ra tiếng nào, và SettingsPanel đã ghi rõ điều đó.
//  - SFX lấy âm lượng từ GameStateManager.SfxVolume mỗi lần phát (đổi
//    slider là nghe ngay, không cần khởi động lại).
//
// Singleton DontDestroyOnLoad (cùng mẫu với GameStateManager). Pool 4
// AudioSource để các tiếng chồng nhau được (vd: đập + chạm sân).
//
// Phụ thuộc: UnityEngine, ShuttleLab.Core (GameStateManager),
// ShuttleLab.Gameplay (ShotType). Không phụ thuộc module game khác.

using System;
using UnityEngine;
using ShuttleLab.Core;
using ShuttleLab.Gameplay;

namespace ShuttleLab.Systems
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        private const int SampleRate = 44100;
        private const int PoolSize = 4;

        private AudioSource[] _sources;
        private int _nextSource;

        // Clip tổng hợp sẵn (sinh một lần ở Awake để không giật khi chơi).
        private AudioClip _hitSmash;
        private AudioClip _hitClear;
        private AudioClip _hitSoft;
        private AudioClip _hitServe;
        private AudioClip _bounce;
        private AudioClip _whistle;
        private AudioClip _click;
        private AudioClip _score;

        private float SfxVolume =>
            GameStateManager.Instance != null ? GameStateManager.Instance.SfxVolume : 0.8f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sources = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }
            BuildClips();
        }

        // --- API public ------------------------------------------------------

        /// <summary>Tiếng vợt theo loại cú đánh (mạnh/nhẹ khác nhau).</summary>
        public void PlayHit(ShotType type)
        {
            switch (type)
            {
                case ShotType.Smash:
                case ShotType.JumpSmash:
                case ShotType.HalfSmash:
                    Play(_hitSmash, 1.0f);
                    break;
                case ShotType.ServeLow:
                case ShotType.ServeHigh:
                case ShotType.ServeFlick:
                    Play(_hitServe, 0.8f);
                    break;
                case ShotType.Drop:
                case ShotType.NetShot:
                case ShotType.NetKill:
                case ShotType.Push:
                case ShotType.Block:
                    Play(_hitSoft, 0.7f);
                    break;
                default: // Clear, Drive, Lift, DefensiveDrive
                    Play(_hitClear, 0.85f);
                    break;
            }
        }

        /// <summary>Tiếng cầu chạm sân (nhỏ, trầm).</summary>
        public void PlayBounce() => Play(_bounce, 0.5f);

        /// <summary>Còi trọng tài: 2 hồi (ngắn + dài).</summary>
        public void PlayWhistle() => Play(_whistle, 0.9f);

        /// <summary>Click UI.</summary>
        public void PlayClick() => Play(_click, 0.6f);

        /// <summary>Chuông khi ghi điểm (2 nốt lên).</summary>
        public void PlayScore() => Play(_score, 0.8f);

        // --- Phát ------------------------------------------------------------

        private void Play(AudioClip clip, float volumeScale)
        {
            if (clip == null || _sources == null || _sources.Length == 0) return;
            float vol = SfxVolume * Mathf.Clamp01(volumeScale);
            if (vol <= 0.001f) return; // tắt tiếng thì khỏi phát
            var src = _sources[_nextSource];
            _nextSource = (_nextSource + 1) % _sources.Length;
            src.clip = clip;
            src.volume = vol;
            src.Play();
        }

        // --- Tổng hợp clip ---------------------------------------------------

        private void BuildClips()
        {
            // Smash: noise burst mạnh + trầm 110Hz (0.16s).
            _hitSmash = Synth("HitSmash", 0.16f, t =>
                NoiseBurst(t, 0.10f, 0.9f, seed: 11)
                + Sine(t, 110f) * ExpDecay(t, 28f) * 0.7f);

            // Clear/Drive: noise vừa + sine 260Hz (0.12s).
            _hitClear = Synth("HitClear", 0.12f, t =>
                NoiseBurst(t, 0.07f, 0.55f, seed: 23)
                + Sine(t, 260f) * ExpDecay(t, 40f) * 0.5f);

            // Drop/Net: tick ngắn, nhẹ (0.07s).
            _hitSoft = Synth("HitSoft", 0.07f, t =>
                NoiseBurst(t, 0.03f, 0.4f, seed: 37)
                + Sine(t, 520f) * ExpDecay(t, 70f) * 0.35f);

            // Serve: pop — sweep 350Hz -> 180Hz + noise ngắn (0.10s).
            _hitServe = Synth("HitServe", 0.10f, t =>
            {
                float freq = Mathf.Lerp(350f, 180f, t / 0.10f);
                return Sine(t, freq) * ExpDecay(t, 45f) * 0.6f
                    + NoiseBurst(t, 0.04f, 0.4f, seed: 51);
            });

            // Bounce: thud trầm 90Hz (0.09s).
            _bounce = Synth("Bounce", 0.09f, t =>
                Sine(t, 90f) * ExpDecay(t, 55f) * 0.6f);

            // Whistle: 2 hồi sine 2300Hz + rung nhẹ (0.65s).
            _whistle = Synth("Whistle", 0.65f, t =>
            {
                bool on = t < 0.18f || (t > 0.26f && t < 0.61f);
                if (!on) return 0f;
                float vib = 1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 30f * t);
                return Sine(t, 2300f * vib) * 0.5f
                    + NoiseBurst(t, 0.65f, 0.08f, seed: 71);
            });

            // Click: blip 1200Hz (0.045s).
            _click = Synth("Click", 0.045f, t =>
                Sine(t, 1200f) * ExpDecay(t, 120f) * 0.6f);

            // Score: 880Hz (0.12s) rồi 1318Hz (0.28s).
            _score = Synth("Score", 0.40f, t =>
                t < 0.12f
                    ? Sine(t, 880f) * ExpDecay(t, 25f) * 0.5f
                    : Sine(t - 0.12f, 1318f) * ExpDecay(t - 0.12f, 14f) * 0.55f);
        }

        /// <summary>Sinh clip mono từ hàm mẫu fn(t) với t tính bằng giây.</summary>
        private AudioClip Synth(string name, float duration, Func<float, float> fn)
        {
            int samples = Mathf.Max(1, (int)(SampleRate * duration));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                data[i] = Mathf.Clamp(fn(t), -1f, 1f);
            }
            var clip = AudioClip.Create(name, samples, channels: 1, SampleRate, stream: false);
            clip.SetData(data, 0);
            return clip;
        }

        // --- Khối sóng cơ bản -------------------------------------------------

        private static float Sine(float t, float freq) =>
            Mathf.Sin(2f * Mathf.PI * freq * t);

        private static float ExpDecay(float t, float rate) =>
            Mathf.Exp(-t * rate);

        /// <summary>
        /// Noise trắng có seed cố định (nghe giống nhau mỗi lần phát),
        /// chỉ tồn tại trong `length` giây đầu rồi tắt.
        /// </summary>
        private static float NoiseBurst(float t, float length, float amp, int seed)
        {
            if (t >= length) return 0f;
            // Hash xác định từ (seed, mẫu) — không dùng Random để tránh
            // tiếng khác nhau mỗi lần phát.
            int n = (int)(t * SampleRate);
            uint h = (uint)(seed * 2654435761u ^ (n + 1) * 40503u);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            float white = (h % 20001u) / 10000f - 1f;
            float env = 1f - t / length; // tắt dần tuyến tính
            return white * amp * env;
        }
    }
}
