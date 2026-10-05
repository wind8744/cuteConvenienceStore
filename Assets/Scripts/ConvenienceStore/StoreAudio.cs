using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 효과음. 음원 파일 없이 짧은 음 몇 개를 이어 붙여 8비트풍 소리를 런타임에 만듭니다.
    /// </summary>
    public class StoreAudio
    {
        private const int SampleRate = 44100;
        private const float Volume = 0.25f;

        private readonly AudioSource _source;
        private readonly AudioSource _music;

        public AudioClip Coin { get; }
        public AudioClip Tip { get; }
        public AudioClip Restock { get; }
        public AudioClip Angry { get; }
        public AudioClip Chime { get; }
        public AudioClip Buy { get; }
        public AudioClip DayEnd { get; }
        public AudioClip Flyer { get; }

        public StoreAudio(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;

            Coin = Tones("Coin", true, (988f, 0.07f), (1319f, 0.2f));
            Tip = Tones("Tip", true, (1319f, 0.06f), (1568f, 0.06f), (2093f, 0.24f));
            Restock = Tones("Restock", false, (330f, 0.05f), (440f, 0.1f));
            Angry = Tones("Angry", true, (196f, 0.12f), (147f, 0.22f));
            Chime = Tones("Chime", false, (784f, 0.16f), (659f, 0.32f));
            Buy = Tones("Buy", true, (523f, 0.06f), (659f, 0.06f), (784f, 0.06f), (1047f, 0.2f));
            DayEnd = Tones("DayEnd", false, (523f, 0.15f), (659f, 0.15f), (784f, 0.15f), (1047f, 0.45f));
            Flyer = Tones("Flyer", false, (660f, 0.05f), (880f, 0.12f));

            _music = host.AddComponent<AudioSource>();
            _music.clip = BuildMusic();
            _music.loop = true;
            _music.volume = 0.5f;
            _music.Play();
        }

        public bool MusicOn => !_music.mute;

        public void ToggleMusic() => _music.mute = !_music.mute;

        /// <summary>
        /// 배경 음악: 8초짜리 느긋한 멜로디를 반복합니다. 한 칸이 8분음표이고 0 은 쉼표입니다.
        /// </summary>
        public static AudioClip BuildMusic()
        {
            const float step = 0.25f;
            const float C5 = 523.25f, D5 = 587.33f, E5 = 659.25f, G5 = 783.99f, A5 = 880f, A4 = 440f;
            float[] melody =
            {
                E5, G5, A5, G5, E5, D5, C5, 0f,
                D5, E5, G5, E5, D5, C5, A4, 0f,
                C5, D5, E5, G5, A5, G5, E5, 0f,
                D5, E5, D5, C5, A4, C5, C5, 0f,
            };
            // 베이스는 반 마디(네 칸)마다 한 음: C - A - F - G
            float[] bass = { 130.81f, 130.81f, 110f, 110f, 87.31f, 87.31f, 98f, 98f };

            int stepSamples = Mathf.RoundToInt(step * SampleRate);
            var data = new float[stepSamples * melody.Length];
            for (int i = 0; i < data.Length; i++)
            {
                int s = i / stepSamples;
                float t = (i % stepSamples) / (float)SampleRate;
                float sample = 0f;
                if (melody[s] > 0f)
                {
                    float w = 2f * Mathf.PI * melody[s] * t;
                    float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-5f * t / step);
                    sample += (Mathf.Sin(w) + 0.25f * Mathf.Sin(2f * w)) * env * 0.16f;
                }
                float bt = (i % (stepSamples * 4)) / (float)SampleRate;
                float benv = Mathf.Min(1f, bt / 0.01f) * Mathf.Exp(-2.5f * bt);
                sample += Mathf.Sin(2f * Mathf.PI * bass[s / 4] * bt) * benv * 0.14f;
                data[i] = sample;
            }

            AudioClip clip = AudioClip.Create("Music", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Play(AudioClip clip, float volume = 1f) => _source.PlayOneShot(clip, volume);

        /// <summary>음(주파수, 길이)을 차례로 이어 붙인 클립. square 면 네모파, 아니면 사인파.</summary>
        public static AudioClip Tones(string name, bool square, params (float freq, float seconds)[] notes)
        {
            float total = 0f;
            foreach (var note in notes) total += note.seconds;
            var data = new float[Mathf.CeilToInt(total * SampleRate)];

            int offset = 0;
            foreach (var (freq, seconds) in notes)
            {
                int count = Mathf.Min(Mathf.RoundToInt(seconds * SampleRate), data.Length - offset);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)SampleRate;
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                    if (square) wave = wave >= 0f ? 0.6f : -0.6f;
                    // 딱 끊기는 소리가 나지 않게 짧게 올리고 길게 줄인다.
                    float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-4f * t / seconds);
                    data[offset + i] = wave * envelope * Volume;
                }
                offset += count;
            }

            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
