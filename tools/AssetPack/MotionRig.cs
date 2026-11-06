using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Author the gait once, then bake small sprites. Runtime needs neither a skeleton nor an image library.
internal sealed class MotionRig
{
    public const double Stride=64;
    private readonly BitmapSource _body, _front, _hind, _tail;
    private static readonly Point Shoulder=new(100,104), Hip=new(160,101);

    public MotionRig(BitmapSource sheet,Func<BitmapSource,BitmapSource> isolate)
    {
        BitmapSource Part(int x,int y,int width,int height)=>isolate(new CroppedBitmap(sheet,new Int32Rect(x,y,width,height)));
        _body=Part(40,10,760,570); _front=Part(880,80,240,520);
        _hind=Part(200,660,300,560); _tail=Part(700,660,450,500);
    }

    public void Pack(Action<string,IReadOnlyList<BitmapSource>,int[]> save)
    {
        foreach(var right in new[]{false,true})
        {
            var side=right?"right":"left";
            void Clip(string name,int count,Func<int,(string Kind,double Phase)> pose)
            {
                var frames=Enumerable.Range(0,count).Select(i=>{ var p=pose(i); return Render(p.Kind,p.Phase,right); }).ToArray();
                save(name+"-"+side,frames,Enumerable.Repeat(16,count).ToArray());
            }
            Clip("walk",60,i=>("walk",i/60.0));
            Clip("stalk",60,i=>("stalk",i/60.0));
            Clip("idle",1,_=>("idle",0));
            Clip("crouch",1,_=>("crouch",0));
            Clip("takeoff",8,i=>("takeoff",i/7.0));
            Clip("air",20,i=>("air",i/19.0));
            Clip("fall",1,_=>("air",.85));
            Clip("land",18,i=>("land",i/17.0));
            Clip("catch",12,i=>("catch",i/11.0));
            Clip("pounce",24,i=>i<6?("takeoff",.5+i/10.0):("air",(i-6)/17.0));
        }
    }

    private BitmapSource Render(string kind,double phase,bool right)
    {
        var visual=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var dc=visual.RenderOpen())
        {
            if(right) dc.PushTransform(new ScaleTransform(-1,1,120,0));
            var crouch=kind is "stalk" or "crouch" or "catch"?1.0:kind=="land"?Math.Sin(Math.PI*Math.Pow(phase,.65)):kind=="takeoff"?Math.Sin(Math.PI*phase):0;
            var dy=14*crouch+(kind is "walk" or "stalk"?-.65*Math.Cos(phase*4*Math.PI):0);
            var tilt=-5*crouch;
            var posture=Matrix.Identity; posture.RotateAt(tilt,152,104); posture.Translate(0,dy);
            var shoulder=posture.Transform(Shoulder); var hip=posture.Transform(Hip);
            dc.PushTransform(new MatrixTransform(posture));
            dc.PushTransform(new RotateTransform(Math.Sin(phase*2*Math.PI-.7)*2,174,103));
            dc.DrawImage(_tail,new Rect(174-(780-700)*.16,103-(755-660)*.16,_tail.PixelWidth*.16,_tail.PixelHeight*.16));
            dc.Pop(); dc.Pop();
            Point Paw(bool front,bool far)
            {
                var baseline=front?98.0:152.0;
                var x=baseline+(far?5:0); var y=164.0;
                if(kind is "walk" or "stalk")
                {
                    // Four separate contacts: RH, RF, LH, LF; support paws counter the exact root travel.
                    var contact=front?(far?.25:.75):(far?0:.5);
                    var p=(phase-contact+1)%1; var duty=kind=="stalk"?.72:.64;
                    var stride=Stride*(kind=="stalk"?.65:1);
                    if(p<duty) x-=stride*(duty/2-p);
                    else { var t=(p-duty)/(1-duty); x-=stride*duty*(-.5+Smooth(t)); y-=Math.Sin(Math.PI*t)*(kind=="stalk"?5:9); }
                }
                else if(kind=="air")
                {
                    var reach=Math.Sin(Math.PI*phase);
                    x+=front?-17*reach:18*Math.Sin(Math.PI*(phase+.1));
                    y-=front?18*reach:9*reach;
                }
                else if(kind=="catch") { x+=front?-9*Math.Sin(phase*Math.PI):0; }
                return new Point(x,y);
            }
            HindLeg(dc,hip+new Vector(4,0),Paw(false,true));
            FrontLeg(dc,shoulder+new Vector(4,0),Paw(true,true));
            HindLeg(dc,hip,Paw(false,false));
            FrontLeg(dc,shoulder,Paw(true,false));
            dc.PushTransform(new MatrixTransform(posture));
            dc.DrawImage(_body,new Rect(120+(40-430)*.16,34.4+10*.16,_body.PixelWidth*.16,_body.PixelHeight*.16));
            dc.Pop();
            if(right) dc.Pop();
        }
        return Raster(visual);
    }

    private void FrontLeg(DrawingContext dc,Point shoulder,Point paw)
    {
        var ankle=paw+new Vector(2,-6);
        var elbow=Joint(shoulder,ankle,29,31,-1);
        Bone(dc,_front,new Point(120,100),new Point(112,272),shoulder,elbow,0,280);
        Bone(dc,_front,new Point(112,272),new Point(103,438),elbow,ankle,264,446);
        Bone(dc,_front,new Point(103,438),new Point(95,475),ankle,paw,430,520);
    }

    private void HindLeg(DrawingContext dc,Point hip,Point paw)
    {
        var hock=paw+new Vector(8,-17); var ankle=paw+new Vector(2,-6);
        var knee=Joint(hip,hock,31,27,1);
        Bone(dc,_hind,new Point(165,120),new Point(105,235),hip,knee,0,243);
        Bone(dc,_hind,new Point(105,235),new Point(185,365),knee,hock,227,373);
        Bone(dc,_hind,new Point(185,365),new Point(127,470),hock,ankle,357,478);
        Bone(dc,_hind,new Point(127,470),new Point(100,515),ankle,paw,462,560);
    }

    private static Point Joint(Point root,Point end,double upper,double lower,int bend)
    {
        var delta=end-root; var distance=Math.Clamp(delta.Length,.001,upper+lower-.01);
        var direction=delta; direction.Normalize();
        var along=(upper*upper-lower*lower+distance*distance)/(2*distance);
        var across=Math.Sqrt(Math.Max(0,upper*upper-along*along));
        return root+direction*along+new Vector(-direction.Y,direction.X)*(across*bend);
    }

    private static void Bone(DrawingContext dc,BitmapSource image,Point a,Point b,Point root,Point tip,double top,double bottom)
    {
        var source=b-a; var target=tip-root;
        var turn=Math.Atan2(target.Y,target.X)-Math.Atan2(source.Y,source.X);
        var scale=target.Length/source.Length;
        var cos=Math.Cos(turn)*scale; var sin=Math.Sin(turn)*scale;
        var matrix=new Matrix(cos,sin,-sin,cos,root.X-a.X*cos+a.Y*sin,root.Y-a.X*sin-a.Y*cos);
        dc.PushTransform(new MatrixTransform(matrix));
        dc.PushClip(new RectangleGeometry(new Rect(0,top,image.PixelWidth,bottom-top)));
        dc.DrawImage(image,new Rect(0,0,image.PixelWidth,image.PixelHeight)); dc.Pop(); dc.Pop();
    }

    public static BitmapSource Held(BitmapSource source,bool right)
    {
        var visual=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var dc=visual.RenderOpen())
        {
            if(right) dc.PushTransform(new ScaleTransform(-1,1,120,0));
            const double scale=.127;
            dc.DrawImage(source,new Rect(120-510*scale,48-365*scale,source.PixelWidth*scale,source.PixelHeight*scale));
            if(right) dc.Pop();
        }
        return Raster(visual);
    }

    private static double Smooth(double t)=>t*t*(3-2*t);
    private static BitmapSource Raster(DrawingVisual visual)
    {
        var bitmap=new RenderTargetBitmap(240,240,96,96,PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}
