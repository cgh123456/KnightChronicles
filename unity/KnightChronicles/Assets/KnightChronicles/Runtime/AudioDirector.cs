using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnightChronicles.Runtime
{
    /// <summary>Original procedural ambience and cues. Kept replaceable by authored audio assets.</summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        private static AudioDirector instance;
        private readonly AudioClip[] themes = new AudioClip[10];
        private readonly AudioClip[] cues = new AudioClip[70];
        private readonly AudioSource[] voices = new AudioSource[8];
        private AudioSource music;
        private int voice, nextTheme, currentTheme = -1;
        private float fade;
        public static void Ensure()
        {
            if (instance != null) return;
            new GameObject("GameAudio", typeof(AudioDirector));
        }
        public static void Cue(int id)
        {
            if (instance == null) return;
            var source = instance.voices[instance.voice++ % 8]; source.clip = instance.cues[Mathf.Clamp(id, 0, 69)];
            source.volume = GameSession.State.EffectsVolume * .35f; source.Play();
        }
        private void Awake()
        {
            instance = this; DontDestroyOnLoad(gameObject);
            music = gameObject.AddComponent<AudioSource>(); music.loop = true;
            for (var i = 0; i < themes.Length; i++) themes[i] = Theme(i);
            for (var i = 0; i < cues.Length; i++) cues[i] = Sound(i);
            for (var i = 0; i < voices.Length; i++) voices[i] = gameObject.AddComponent<AudioSource>();
            SceneManager.sceneLoaded += Changed; Changed(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private void Changed(Scene scene, LoadSceneMode mode)
        {
            nextTheme = scene.name == "Lobby" ? 0 : GameSession.State.ActiveRun == null
                ? GameSession.State.ReportPending && GameSession.State.LastReport != null ? GameSession.State.LastReport.Extracted ? 8 : 9 : 1
                : GameSession.State.ActiveRun.Floor + 1;
            fade = 0;
        }
        public static void FloorChanged() { if (instance != null) instance.Changed(SceneManager.GetActiveScene(), LoadSceneMode.Single); }
        public static void Threat(bool elite)
        {
            if(instance==null || GameSession.State.ActiveRun==null)return;
            var theme=elite?7:GameSession.State.ActiveRun.Floor+1;
            if(instance.nextTheme!=theme){instance.nextTheme=theme;instance.fade=0;}
        }
        private void Update()
        {
            AudioListener.volume = GameSession.State.MasterVolume;
            if (currentTheme != nextTheme)
            {
                fade += Time.unscaledDeltaTime * 3; music.volume = Mathf.Lerp(GameSession.State.MusicVolume * .25f, 0, fade);
                if (fade >= 1) { currentTheme = nextTheme; music.clip = themes[currentTheme]; music.Play(); fade = 0; }
            }
            else { fade = Mathf.Min(1, fade + Time.unscaledDeltaTime * 3); music.volume = GameSession.State.MusicVolume * .25f * fade; }
        }
        private void OnDestroy() { SceneManager.sceneLoaded -= Changed; if (instance == this) instance = null; }
        private static AudioClip Theme(int variant)
        {
            const int sampleRate = 22050; const int seconds = 16;
            var samples = new float[sampleRate * seconds];
            var root = 110 * Mathf.Pow(2, (variant % 5) / 12f);
            var notes = variant <= 1 ? new[] { 0, 7, 12, 4, 9, 7, 4, 2 } : new[] { 0, 7, 3, 10, 5, 3, 7, 2 };
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate; var note = Mathf.FloorToInt(t * 2) % notes.Length;
                var f = root * Mathf.Pow(2, notes[note] / 12f); var beat = t * 2 % 1;
                var envelope = Mathf.Sin(beat * Mathf.PI) * Mathf.Exp(-beat * 3);
                var drone = Mathf.Sin(t * root * Mathf.PI * 2) * .07f + Mathf.Sin(t * root * .5f * Mathf.PI * 2) * .05f;
                samples[i] = (Mathf.Sin(t * f * Mathf.PI * 2) + .2f * Mathf.Sin(t * f * Mathf.PI * 4)) * envelope * .12f + drone;
            }
            var clip = AudioClip.Create("Theme_" + variant, samples.Length, 1, sampleRate, false); clip.SetData(samples, 0); return clip;
        }
        private static AudioClip Sound(int variant)
        {
            const int rate = 22050; var duration = .10f + (variant % 5) * .045f; var samples = new float[(int)(rate * duration)];
            var f = 180 + variant * 17; var phase = 0f;
            for (var i = 0; i < samples.Length; i++) { var t = (float)i / rate; var ratio = t / duration;
                phase += 2 * Mathf.PI * (f * (variant % 2 == 0 ? 1 - ratio * .5f : 1 + ratio)) / rate;
                samples[i] = Mathf.Sin(phase) * Mathf.Sin(ratio * Mathf.PI) * (1 - ratio) * .4f; }
            var clip = AudioClip.Create("Cue_" + variant, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
    }
}
