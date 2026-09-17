using System;
using System.Collections;
using System.IO;
using KnightChronicles.Runtime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnightChronicles.Runtime
{
    /// <summary>Opt-in built-player smoke check; normal play never enters this path or touches its isolated save.</summary>
    public sealed class BuildSmokeRunner : MonoBehaviour
    {
        private static string directory;
        private string failure;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); var flag = Array.IndexOf(args, "--knight-smoke");
            if (flag < 0) return;
            directory = flag + 1 < args.Length ? Path.GetFullPath(args[flag + 1]) : Path.Combine(Path.GetTempPath(), "KnightChronicles-BuildSmoke");
            Directory.CreateDirectory(directory); GameSession.BeginIsolatedTestSession(directory);
            var node = new GameObject("BuildSmokeRunner", typeof(BuildSmokeRunner)); DontDestroyOnLoad(node);
        }
        private void Awake() { Application.logMessageReceived += Log; }
        private void OnDestroy() { Application.logMessageReceived -= Log; }
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failure = message + "\n" + stack;
        }
        private void Check(bool ok, string label) { if (!ok) failure = (failure ?? "") + "\n" + label; }
        private IEnumerator Start()
        {
            yield return null;
            Check(GameObject.Find("LobbyKnight") != null, "Lobby knight missing");
            Check(GameSession.Rules.Catalog.All.Count == 654, "Catalog count");
            string error = null; Check(GameSession.Commit(r => r.BeginRun(0, 20260917, out error)), "Begin: " + error);
            SceneManager.LoadScene("Town"); for (var i = 0; i < 5; i++) yield return null;
            var dungeon = FindObjectOfType<DungeonController>(); Check(dungeon != null, "Dungeon missing");
            if (dungeon != null)
            {
                var enemy = dungeon.Floor.Enemies[0]; dungeon.TeleportForTest(new Vector2(enemy.X, enemy.Y + 1)); dungeon.Strike(3,1000,-1); yield return null;
                Check(enemy.Dead && dungeon.Floor.Drops.Count > 0,"Real attack/loot");
                for (var i = 0; i < 4; i++) { dungeon.Travel(1); yield return null; }
                dungeon.Travel(-1); yield return null;
                Check(GameSession.State.ActiveRun.Floor==4,"Backtracking");
                GamePanel.OpenPage("inventory"); for(var i=0;i<4;i++) yield return null;
                Capture("inventory.png");
                var screenshot=Path.Combine(directory,"inventory-screen.png"); ScreenCapture.CaptureScreenshot(screenshot);
                for(var i=0;i<5;i++) yield return null;
                dungeon.End(true); for(var i=0;i<4;i++) yield return null;
                Check(GameSession.State.ActiveRun==null && GameSession.State.CareerLevel==2,"Extraction settlement");
                GamePanel.OpenPage("report"); for(var i=0;i<4;i++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"report-screen.png"));
                for(var i=0;i<5;i++) yield return null;
            }
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),failure==null?"PASS: lobby, catalog, real melee/loot, five floors, backtracking, inventory, extraction and persisted career level.":"FAIL: "+failure);
            Application.Quit(failure==null?0:1);
        }
        private void Capture(string name)
        {
            var c=Camera.main; if(c==null)return;
            var rt=new RenderTexture(1600,900,24); var old=c.targetTexture; var oldActive=RenderTexture.active;
            c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(directory,name),image.EncodeToPNG());c.targetTexture=old;RenderTexture.active=oldActive;
            Destroy(image);Destroy(rt);
        }
    }
}
