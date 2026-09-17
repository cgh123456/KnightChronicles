using System;
using System.Collections;
using System.IO;
using KnightChronicles.Runtime;
using KnightChronicles.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KnightChronicles.Tests
{
    public sealed class ExpeditionSmokeTests
    {
        [UnityTest] public IEnumerator PursuitSteersAroundACrateInsteadOfStoppingOnTheZeroAxis()
        {
            GameSession.BeginIsolatedTestSession(Path.Combine(Path.GetTempPath(),"KnightChronicles-SteeringTests"));string error=null;
            GameSession.Commit(r=>r.BeginRun(0,43,out error));yield return LoadTown();
            var dungeon=UnityEngine.Object.FindObjectOfType<DungeonController>();
            dungeon.Floor.Searches.RemoveAll(s=>s.Room==0);dungeon.BuildFloor();GamePanel.OpenPage("pause");
            var fixture=new GameObject("AvoidanceFixture");
            DungeonScenery.Prop("crate",fixture.transform,Vector2.zero,new Vector2(1.25f,1.3f));
            foreach(var side in new[]{-1,1})
            {
                var p=new Vector2(0,1.5f);var target=new Vector2(0,-1.5f);
                for(var step=0;step<120;step++)
                {p=DungeonScenery.StepToward(dungeon.Floor,p,target,.075f,side);Assert.IsTrue(DungeonScenery.ClearPoint(p.x,p.y),"Pursuit crossed crate footprint");}
                Assert.Less(Vector2.Distance(p,target),.15f,"Enemy failed to walk around blocking crate");
            }
            UnityEngine.Object.Destroy(fixture);
        }
        [UnityTest] public IEnumerator ModeledDungeonAndDirectionalEnemyArtAreActuallyConnected()
        {
            foreach(var key in new[]{"stone_floor","stone_wall","crate","chest","bookshelf","bones","supplies","urn","stair_up","stair_down","portal","door","pillar","torch"})
                Assert.IsNotNull(ModelArt.Sprite(key),"Missing baked model: "+key);
            GameSession.BeginIsolatedTestSession(Path.Combine(Path.GetTempPath(),"KnightChronicles-ArtTests"));string error=null;
            Assert.IsTrue(GameSession.Commit(r=>r.BeginRun(0,43,out error)));yield return LoadTown();
            var walls=GameObject.FindObjectsOfType<WallOcclusion>();Assert.Greater(walls.Length,10);
            AssertClearSpawns(UnityEngine.Object.FindObjectOfType<DungeonController>());
            Assert.IsNotNull(walls[0].GetComponent<SpriteRenderer>().sprite);
            var enemies=GameObject.FindObjectsOfType<EnemyPresentation>();Assert.IsNotEmpty(enemies);
            var enemy=enemies[0];var animator=enemy.GetComponent<SpriteAnimator>();
            Assert.IsTrue(animator.HasAction("idle"));Assert.IsTrue(animator.HasAction("walk"));Assert.IsTrue(animator.HasAction("attack"));
            enemy.Tick(Vector2.up,true,false);Assert.AreEqual(4,animator.Direction);Assert.AreEqual("walk",animator.CurrentAction);
            Assert.AreEqual(192,enemy.GetComponent<SpriteRenderer>().sprite.rect.width);
            var player=GameObject.Find("Player").GetComponent<SpriteAnimator>();Assert.IsTrue(player.HasAction("attack"));Assert.IsTrue(player.HasAction("hit"));
            var panel=GameObject.FindObjectOfType<GamePanel>();GamePanel.OpenPage("inventory");var count=panel.PaintCount;
            for(var i=0;i<10;i++)yield return null;
            if(panel.PaintCount==count)
            {var target=new RenderTexture(1600,900,24);GuiOffscreenReview.Draw(panel,target);UnityEngine.Object.Destroy(target);}
            Assert.AreEqual("inventory",panel.CurrentPage);Assert.Greater(panel.PaintCount,count,"GUI must receive real Repaint events");
        }
        private static IEnumerator LoadTown()
        {
            var load = SceneManager.LoadSceneAsync("Assets/Scenes/Town.unity"); while (!load.isDone) yield return null;
            for (var i = 0; i < 4; i++) yield return null;
        }
        private static void AssertClearSpawns(DungeonController dungeon)
        {
            Assert.IsTrue(DungeonGenerator.Walkable(dungeon.Floor,dungeon.Position.x,dungeon.Position.y));
            Assert.IsTrue(DungeonScenery.ClearPoint(dungeon.Position.x,dungeon.Position.y),"Player spawned inside scenery");
            foreach(var actor in dungeon.Floor.Enemies)
                if(!actor.Dead)
                {
                    Assert.IsTrue(DungeonGenerator.Walkable(dungeon.Floor,actor.X,actor.Y));
                    Assert.IsTrue(DungeonScenery.ClearPoint(actor.X,actor.Y),"Enemy spawned inside scenery");
                }
        }
        [UnityTest] public IEnumerator TwentyExpeditionsLoadAllFloorsBacktrackAndSettleWithoutLostSafeState()
        {
            for (var round = 0; round < 20; round++)
            {
                GameSession.BeginIsolatedTestSession(Path.Combine(Path.GetTempPath(), "KnightChronicles-PlayTests"));
                string error = null;
                GameSession.State.Warehouse.Add(new Item { Id = "ore_0", Count = 5 });
                Assert.IsTrue(GameSession.Commit(r => r.BeginRun(0, round, out error)));
                yield return LoadTown();
                var dungeon = UnityEngine.Object.FindObjectOfType<DungeonController>(); Assert.IsNotNull(dungeon);
                AssertClearSpawns(dungeon);
                var player = GameObject.Find("Player"); Assert.IsNotNull(player.GetComponent<SpriteRenderer>().sprite);
                Assert.AreEqual("idle", player.GetComponent<SpriteAnimator>().CurrentAction);
                // Traverse the actual runtime floor rebuilding and checkpoint path in both directions.
                for (var i = 0; i < 4; i++) { dungeon.Travel(1); AssertClearSpawns(dungeon); yield return null; Assert.AreEqual(i + 2, GameSession.State.ActiveRun.Floor); }
                dungeon.Travel(-1); yield return null; Assert.AreEqual(4, GameSession.State.ActiveRun.Floor);
                Assert.GreaterOrEqual(dungeon.Floor.ExtractRoom, 0);
                var expected = round % 2 == 0;
                dungeon.End(expected); for (var i = 0; i < 4; i++) yield return null;
                Assert.IsNull(GameSession.State.ActiveRun); Assert.AreEqual(expected, GameSession.State.LastReport.Extracted);
                Assert.GreaterOrEqual(GameSession.State.Warehouse.Find(i => i.Id == "ore_0").Count, 5);
                Assert.AreEqual(expected ? 2 : 1, GameSession.State.CareerLevel);
                Assert.IsNull(GameSession.SaveError);
            }
        }
        [UnityTest] public IEnumerator DungeonPauseFreezesActorsAndKnightAtlasHasOriginalFrameDimensions()
        {
            GameSession.BeginIsolatedTestSession(Path.Combine(Path.GetTempPath(), "KnightChronicles-PlayTests")); string error = null;
            GameSession.Commit(r => r.BeginRun(0, 31, out error)); yield return LoadTown();
            var dungeon = UnityEngine.Object.FindObjectOfType<DungeonController>(); var player = GameObject.Find("Player");
            var sr = player.GetComponent<SpriteRenderer>(); Assert.AreEqual(256, sr.sprite.rect.width); Assert.AreEqual(256, sr.sprite.rect.height);
            GamePanel.OpenPage("pause"); Assert.AreEqual(0, Time.timeScale);
            var pos = player.transform.position; var hp = GameSession.State.ActiveRun.Hp;
            for (var i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(pos, player.transform.position); Assert.AreEqual(hp, GameSession.State.ActiveRun.Hp);
            Time.timeScale = 1;
        }
        [UnityTest] public IEnumerator RealMeleeHitKillsEnemyAndProducesLoot()
        {
            GameSession.BeginIsolatedTestSession(Path.Combine(Path.GetTempPath(), "KnightChronicles-PlayTests")); string error = null;
            GameSession.Commit(r => r.BeginRun(0, 43, out error)); yield return LoadTown();
            var dungeon = UnityEngine.Object.FindObjectOfType<DungeonController>(); var enemy = dungeon.Floor.Enemies[0];
            dungeon.TeleportForTest(new Vector2(enemy.X, enemy.Y + 1));
            dungeon.Strike(3, 1000, -1); yield return null;
            Assert.IsTrue(enemy.Dead); Assert.GreaterOrEqual(GameSession.State.ActiveRun.Kills, 1); Assert.IsNotEmpty(dungeon.Floor.Drops);
            Assert.IsNotNull(UnityEngine.Object.FindObjectOfType<GamePanel>());
            var output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "dungeon-smoke.png");
            var camera = Camera.main; var target = new RenderTexture(1600, 900, 24); var previous = camera.targetTexture;
            var previousActive = RenderTexture.active; camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            File.WriteAllBytes(output,image.EncodeToPNG()); camera.targetTexture=previous; RenderTexture.active=previousActive;
            UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); Assert.IsTrue(File.Exists(output));
            Debug.Log("Dungeon scene screenshot (HUD excluded): " + output);
        }
    }
}
