// 代码由 AI（DeepSeek V4, Claude Code）生成，非人类手写。https://github.com/Yuxcv/nx-ai-tools
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using NXOpen;
using NXOpen.UF;

public class C
{
    static Session _s; static UFSession _uf; static Part _wp; static Tag _mtx;
    static Tag _lastBody, _prevBody;
    const string TMPL = @"E:\UGII\templates\model-plain-1-mm-template.prt";
    const string CMDF = @"C:\temp\nx\cmd_new.txt";
    const string RESF = @"C:\temp\nx\res.txt";
    const string OUT = @"C:\Users\Administrator\Desktop\nx_output.prt";

    // ========== 初始化 ==========
    static void InitNX(){
        _s = (Session)Activator.GetObject(typeof(Session), "http://127.0.0.1:4567/NXOpenSession");
        _uf = (UFSession)Activator.GetObject(typeof(UFSession), "http://127.0.0.1:4567/UFSession");
        for(int i=0; i<20; i++){
            try{ _s.ApplicationSwitchImmediate("UG_APP_MODELING"); break; }
            catch{ Thread.Sleep(3000); }
        }
        if(_s.Parts.Work == null) NewCanvas();
        _wp = _s.Parts.Work;
        RefreshWcs();
        Log("就绪");
    }
    static void NewCanvas(){
        string cv = @"C:\temp\nx\c" + DateTime.Now.Ticks + ".prt";
        File.Copy(TMPL, cv);
        PartLoadStatus st; _s.Parts.OpenBaseDisplay(cv, out st);
        _lastBody = Tag.Null; _prevBody = Tag.Null;
    }
    static void RefreshWcs(){
        Tag w; _uf.Csys.AskWcs(out w); _uf.Csys.AskMatrixOfObject(w, out _mtx);
    }

    // ========== 主循环 ==========
    static void Main()
    {
        InitNX();

        var handlers = new Dictionary<string, Action<string[]>>{
            {"plate",cmd_plate},{"block",cmd_block},{"b",cmd_block},
            {"cylinder",cmd_cyl},{"c",cmd_cyl},{"sphere",cmd_sphere},{"s",cmd_sphere},
            {"cone",cmd_cone},{"sketch_rect",cmd_sketch_rect},{"sketch_circle",cmd_sketch_circle},
            {"extrude",cmd_extrude},{"pocket",cmd_pocket},{"hole",cmd_hole},
            {"subtract",cmd_subtract},{"unite",cmd_unite},{"merge",cmd_unite},
            {"clear",cmd_clear},{"save",cmd_save},{"pause",cmd_pause},{"exec",cmd_exec},
        };

        string last = "";
        while(true){
            if(!File.Exists(CMDF)){Thread.Sleep(100); continue;}
            try{
                string line = ReadFile(CMDF);
                if(line == "" || line == last) continue;
                last = line;
                string[] p = line.Split(' ');
                string cmd = p[0];
                if(cmd == "q") break;

                Action<string[]> fn;
                if(!handlers.TryGetValue(cmd, out fn)) continue;

                try{ fn(p); _uf.Disp.Refresh(); WriteRes("OK"); }
                catch(Exception ex){
                    string err = ex.Message;
                    if(ex.InnerException != null) err = ex.InnerException.Message;
                    WriteRes("ERR: " + err); Log("ERR: " + err);
                }
            }catch{Thread.Sleep(500);}
            Thread.Sleep(50);
        }
    }

    // ========== 命令处理 ==========
    static void cmd_plate(string[] p){
        double ox=G(p,1,-35),oy=G(p,2,-15),w=G(p,3,70),h=G(p,4,30),d=G(p,5,5);
        SetBody(MakeBlock(ox,oy,0,w,h,d)); Log("plate "+w+"x"+h+"x"+d);
    }
    static void cmd_block(string[] p){
        double x=G(p,1,0),y=G(p,2,0),w=G(p,3,100),h=G(p,4,50),d=G(p,5,30);
        SetBody(MakeBlock(x,y,G(p,6,0),w,h,d)); Log("block");
    }
    static void cmd_cyl(string[] p){
        double d=G(p,1,50),h=G(p,2,100),x=G(p,3,0),y=G(p,4,0),z=G(p,5,0);
        SetBody(MakeCylinder(d,h,x,y,z,0,0,1)); Log("cyl");
    }
    static void cmd_sphere(string[] p){
        double d=G(p,1,50),x=G(p,2,0),y=G(p,3,0),z=G(p,4,50);
        SetBody(MakeSphere(d,x,y,z)); Log("sphere");
    }
    static void cmd_cone(string[] p){
        double bd=G(p,1,50),td=G(p,2,20),h=G(p,3,80),x=G(p,4,0),y=G(p,5,0),z=G(p,6,0);
        SetBody(MakeCone(bd,td,h,x,y,z)); Log("cone");
    }
    static void cmd_sketch_rect(string[] p){
        SketchRect(G(p,1,0),G(p,2,0),0,G(p,3,70),G(p,4,30)); Log("sketch rect");
    }
    static void cmd_sketch_circle(string[] p){
        double r=G(p,4,30); SketchCircle(G(p,1,0),G(p,2,0),G(p,3,0),r);
        Log("sketch circle r="+r);
    }
    static void cmd_extrude(string[] p){
        double x=G(p,1,0),y=G(p,2,0),z=G(p,3,0),w=G(p,4,50),h=G(p,5,50),d=G(p,6,20);
        var sign = G(p,7,0)==1?FeatureSigns.Positive:G(p,7,0)==2?FeatureSigns.Negative:FeatureSigns.Nullsign;
        Tag[] tags = SketchRect(x,y,z,w,h);
        SetBody(Extrude(tags,d,x+w/2,y+h/2,z,0,0,1,sign));
        Log("extrude "+w+"x"+h+"x"+d);
    }
    static void cmd_pocket(string[] p){
        double x=G(p,1,0),y=G(p,2,0),z=G(p,3,0),r=G(p,4,15),d=G(p,5,20);
        Extrude(new[]{SketchCircle(x,y,z,r)},d,x,y,z,0,0,1,FeatureSigns.Negative);
        Log("pocket r="+r+" depth="+d);
    }
    static void cmd_hole(string[] p){
        double x=G(p,1,0),y=G(p,2,0),r=G(p,3,6),d=G(p,4,15);
        SketchCircle(x,y,5,r);
        BoolSub(_lastBody, MakeCylinder(r*2,d,x,y,5,0,0,-1));
        Log("hole d="+(r*2)+" depth="+d);
    }
    static void cmd_subtract(string[] p){
        double d=G(p,3,20); BoolSub(_lastBody, MakeCylinder(d,G(p,4,30),G(p,1,0),G(p,2,0),G(p,5,0),0,0,1));
        Log("subtract d="+d);
    }
    static void cmd_unite(string[] p){
        BoolUnite(_prevBody, _lastBody);
        if(_prevBody != Tag.Null){_lastBody = _prevBody; _prevBody = Tag.Null;}
        Log("united");
    }
    static void cmd_clear(string[] p){
        NewCanvas(); _wp = _s.Parts.Work; RefreshWcs(); Log("cleared");
    }
    static void cmd_exec(string[] p){
        try{
            object[] args = new object[0];
            // className=null, methodName="main" for Python scripts
            _s.Execute(@"C:\Users\Administrator\Desktop\nx_step.py", "", "main", args);
            Log("exec ok");
        }catch(Exception ex){
            Log("exec err: "+ex.Message);
            if(ex.InnerException!=null) Log("inner: "+ex.InnerException.Message);
        }
    }
    static void cmd_pause(string[] p){
        int ms = (int)(G(p,1,1000)); // 默认1秒
        Thread.Sleep(ms);
        Log("pause " + ms + "ms");
    }
    static void cmd_save(string[] p){
        _uf.Part.Save();
        try{File.Copy(_wp.FullPath, OUT, true);}catch{} Log("saved");
    }

    // ========== 快捷方法 ==========
    static Point3d P(double x,double y,double z){return new Point3d(x,y,z);}
    static Vector3d V(double x,double y,double z){return new Vector3d(x,y,z);}
    static double G(string[] p,int i,double def){return p.Length>i?double.Parse(p[i]):def;}
    static void SetBody(Tag t){_prevBody=_lastBody;_lastBody=t;}
    static void BoolSub(Tag a,Tag b){
        if(a==Tag.Null||b==Tag.Null)return;
        Tag e;_uf.Modl.SubtractBodiesWithRetainedOptions(a,b,false,true,out e);
    }
    static void BoolUnite(Tag a,Tag b){
        if(a==Tag.Null||b==Tag.Null)return;
        Tag e;_uf.Modl.UniteBodiesWithRetainedOptions(a,b,false,false,out e);
    }
    static Tag BodyTag(NXOpen.Features.Feature f){
        Tag t;_uf.Modl.AskFeatBody(f.Tag,out t);return t;
    }

    // ========== 基础体 ==========
    static Tag MakeBlock(double x,double y,double z,double w,double h,double d){
        var b=_wp.Features.CreateBlockFeatureBuilder(null);
        try{b.SetOriginAndLengths(P(x,y,z),w.ToString(),h.ToString(),d.ToString());return BodyTag(b.CommitFeature());}
        finally{b.Destroy();}
    }
    static Tag MakeCylinder(double diam,double h,double x,double y,double z,double dx,double dy,double dz){
        var b=_wp.Features.CreateCylinderBuilder(null);
        try{b.Diameter.RightHandSide=diam.ToString();b.Height.RightHandSide=h.ToString();b.Origin=P(x,y,z);b.Direction=V(dx,dy,dz);return BodyTag(b.CommitFeature());}
        finally{b.Destroy();}
    }
    static Tag MakeSphere(double diam,double x,double y,double z){
        NXOpen.Features.Sphere ns=null;var b=_wp.Features.CreateSphereBuilder(ns);
        try{b.CenterPoint=_wp.Points.CreatePoint(P(x,y,z));b.Diameter.RightHandSide=diam.ToString();return BodyTag(b.CommitFeature());}
        finally{b.Destroy();}
    }
    static Tag MakeCone(double bd,double td,double h,double x,double y,double z){
        NXOpen.Features.Cone nc=null;var b=_wp.Features.CreateConeBuilder(nc);
        try{b.BaseDiameter.RightHandSide=bd.ToString();b.TopDiameter.RightHandSide=td.ToString();b.Height.RightHandSide=h.ToString();b.Axis=_wp.Axes.CreateAxis(P(x,y,z),V(0,0,1),SmartObject.UpdateOption.WithinModeling);return BodyTag(b.CommitFeature());}
        finally{b.Destroy();}
    }

    // ========== 草图 ==========
    static void ActivateSketch(double x,double y,double z,out Sketch sk){
        var sb=_wp.Sketches.CreateSketchInPlaceBuilder2(null);
        try{sb.PlaneOption=NXOpen.Sketch.PlaneOption.Inferred;sb.SketchOrigin=_wp.Points.CreatePoint(P(x,y,z));sk=(NXOpen.Sketch)sb.Commit();}
        finally{sb.Destroy();}
        sk.Activate(NXOpen.Sketch.ViewReorient.False);
    }
    static void DeactivateSketch(Sketch sk){
        sk.Deactivate(NXOpen.Sketch.ViewReorient.False,NXOpen.Sketch.UpdateLevel.SketchOnly);
    }
    static Tag SketchCircle(double cx,double cy,double cz,double r){
        Sketch sk;ActivateSketch(cx,cy,cz,out sk);
        try{
            var a=new UFCurve.Arc();
            a.arc_center=new double[]{cx,cy,cz};a.radius=r;a.start_angle=0;a.end_angle=2*Math.PI;a.matrix_tag=_mtx;
            Tag t;_uf.Curve.CreateArc(ref a,out t);return t;
        }finally{DeactivateSketch(sk);}
    }
    static Tag[] SketchRect(double x,double y,double z,double w,double h){
        Sketch sk;ActivateSketch(x,y,z,out sk);
        try{
            var tags=new Tag[4];
            double[][] pts={new[]{x,y,z},new[]{x+w,y,z},new[]{x+w,y+h,z},new[]{x,y+h,z}};
            for(int i=0;i<4;i++){
                var ln=new UFCurve.Line();
                ln.start_point=pts[i];ln.end_point=pts[(i+1)%4];
                Tag t;_uf.Curve.CreateLine(ref ln,out t);tags[i]=t;
            }
            return tags;
        }finally{DeactivateSketch(sk);}
    }

    // ========== 拉伸 ==========
    static Tag Extrude(Tag[] curves,double dist,double px,double py,double pz,double dx,double dy,double dz,FeatureSigns sign){
        Tag[] feats;
        _uf.Modl.CreateExtruded(curves,"0",new[]{"0",dist.ToString()},new[]{px,py,pz},new[]{dx,dy,dz},sign,out feats);
        if(feats!=null&&feats.Length>0){Tag bt;_uf.Modl.AskFeatBody(feats[0],out bt);return bt;}
        return Tag.Null;
    }

    // ========== IO ==========
    static void Log(string msg){Console.WriteLine("[C] "+msg);}
    static void WriteRes(string msg){try{File.WriteAllText(RESF,msg);}catch{}}
    static string ReadFile(string path){
        try{using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var sr=new StreamReader(fs))return sr.ReadToEnd().Trim();}
        catch{return"";}
    }
}
