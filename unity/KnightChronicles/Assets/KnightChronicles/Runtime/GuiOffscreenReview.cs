using System;
using System.Reflection;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Review-only native IMGUI container for real panel rendering without a visible swapchain.</summary>
    public static class GuiOffscreenReview
    {
        public static void Draw(GamePanel panel,RenderTexture target)
        {
            var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var utility=typeof(GUIUtility);var stateType=utility.Assembly.GetType("UnityEngine.ObjectGUIState");
            var state=Activator.CreateInstance(stateType,true);
            var begin=utility.GetMethod("BeginContainer",flags);var start=utility.GetMethod("BeginGUI",flags);
            var end=utility.GetMethod("EndContainer",flags);
            var layout=typeof(GUILayoutUtility).GetMethod("Layout",flags);
            var paint=typeof(GamePanel).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=RenderTexture.active;var oldCapture=GamePanel.CaptureTarget;
            try
            {
                foreach(var type in new[]{EventType.Layout,EventType.Repaint})
                {
                    begin.Invoke(null,new[]{state});
                    try
                    {
                        Event.current=new Event{type=type,mousePosition=new Vector2(-10000,-10000)};
                        start.Invoke(null,new object[]{0,panel.GetInstanceID(),1});
                        Graphics.SetRenderTarget(target);GamePanel.CaptureTarget=target;
                        GL.PushMatrix();
                        try {GL.LoadPixelMatrix(0,target.width,target.height,0);paint.Invoke(panel,null);
                            if(type==EventType.Layout)layout.Invoke(null,null);}
                        finally{GL.PopMatrix();}
                    }
                    finally{end.Invoke(null,null);}
                }
            }
            finally
            {
                GamePanel.CaptureTarget=oldCapture;Graphics.SetRenderTarget(previous);
                var disposable=state as IDisposable;if(disposable!=null)disposable.Dispose();
            }
        }
    }
}
