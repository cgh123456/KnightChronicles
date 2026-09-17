using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnightChronicles.Runtime
{
    /// <summary>Opt-in isolated visual review; never loads or changes the player's actual profile.</summary>
    public sealed class ArtScreenshotRunner : MonoBehaviour
    {
        private static string output;
        private string failure;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();var flag=Array.IndexOf(args,"--knight-artcheck");if(flag<0)return;
            output=flag+1<args.Length?Path.GetFullPath(args[flag+1]):Path.Combine(Path.GetTempPath(),"KnightChronicles-ArtReview");
            Directory.CreateDirectory(output);GameSession.BeginIsolatedTestSession(output);
            Application.runInBackground=true;
            var node=new GameObject("ArtScreenshotRunner",typeof(ArtScreenshotRunner));DontDestroyOnLoad(node);
        }
        private void Awake(){Application.logMessageReceived+=Log;}
        private void OnDestroy(){Application.logMessageReceived-=Log;}
        private void Log(string message,string stack,LogType type)
        {if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)failure=message+"\n"+stack;}
        private IEnumerator Start()
        {
            for(var i=0;i<10;i++)yield return null;
            yield return Capture("lobby.png");
            SceneManager.LoadScene("Town");for(var i=0;i<30;i++)yield return null;
            yield return Capture("town.png");
            yield return TownView("town-north.png",new Vector2(0,6),new Vector2(0,7.5f),8.5f);
            yield return TownView("town-guild.png",new Vector2(13.2f,-7.35f),new Vector2(10.6f,-4),6.5f);
            GamePanel.OpenPage("inventory");yield return Capture("town-inventory.png");
            string error=null;GameSession.Commit(r=>r.BeginRun(0,170926,out error));
            SceneManager.LoadScene("Town");for(var i=0;i<15;i++)yield return null;
            var dungeon=FindObjectOfType<DungeonController>();
            yield return Capture("dungeon-entry.png");
            if(dungeon!=null)
            {
                var enemy=dungeon.Floor.Enemies[0];dungeon.TeleportForTest(new Vector2(enemy.X,enemy.Y-2));
                for(var i=0;i<5;i++)yield return null;
                yield return Capture("dungeon-enemy.png");
                dungeon.Strike(3,1000,-1);yield return Capture("combat-loot.png");
                GamePanel.OpenPage("inventory");yield return Capture("dungeon-inventory.png");
                GamePanel.OpenPage("");
                for(var floor=2;floor<=5;floor++)
                {dungeon.Travel(1);yield return Capture("dungeon-floor-"+floor+".png");}
                dungeon.Travel(-1);
                GamePanel.OpenPage("help");yield return Capture("dungeon-guide.png");
                dungeon.End(true);for(var i=0;i<15;i++)yield return null;
                yield return Capture("extraction-report.png");
            }
            var panel=FindObjectOfType<GamePanel>();
            File.WriteAllText(Path.Combine(output,"art-review-result.txt"),failure==null?"PASS runtime; GUI Repaint count: "+(panel==null?0:panel.PaintCount):"FAIL: "+failure);
            Application.Quit(failure==null?0:1);
        }
        private IEnumerator TownView(string file,Vector2 position,Vector2 cameraPosition,float zoom)
        {
            var player=GameObject.FindGameObjectWithTag("Player");var body=player.GetComponent<Rigidbody2D>();
            body.position=position;body.velocity=Vector2.zero;player.transform.position=position;
            var camera=Camera.main;var follow=camera.GetComponent<CameraFollow2D>();follow.enabled=false;
            camera.transform.position=new Vector3(cameraPosition.x,cameraPosition.y,-10);camera.orthographicSize=zoom;
            for(var i=0;i<10;i++)yield return null;
            yield return Capture(file);
            follow.enabled=true;
        }
        private IEnumerator Capture(string file)
        {
            var camera=Camera.main;if(camera==null)yield break;
            // Overlay UGUI (the lobby) must render through the review camera to reach its target.
            var overlays=new List<Canvas>();var distances=new List<float>();var cameras=new List<Camera>();
            foreach(var canvas in FindObjectsOfType<Canvas>())
                if(canvas.renderMode==RenderMode.ScreenSpaceOverlay)
                {overlays.Add(canvas);distances.Add(canvas.planeDistance);cameras.Add(canvas.worldCamera);
                 canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            Canvas.ForceUpdateCanvases();
            var target=new RenderTexture(Screen.width,Screen.height,24);var old=camera.targetTexture;
            camera.targetTexture=target;camera.Render();camera.targetTexture=old;
            var panel=FindObjectOfType<GamePanel>();var start=panel==null?0:panel.PaintCount;
            GamePanel.CaptureTarget=target;
            for(var i=0;i<8;i++) {camera.targetTexture=target;camera.Render();camera.targetTexture=old;yield return null;}
            GamePanel.CaptureTarget=null;
            if(panel!=null&&panel.PaintCount==start)GuiOffscreenReview.Draw(panel,target);
            var active=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());
            RenderTexture.active=active;Destroy(image);Destroy(target);
            for(var i=0;i<overlays.Count;i++)
            {overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];}
            Debug.Log("Art review "+file+"; GUI repaint delta "+(panel==null?0:panel.PaintCount-start));
        }
    }
}
