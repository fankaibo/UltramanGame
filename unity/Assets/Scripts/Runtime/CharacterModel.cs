using UnityEngine;
using static UltramanGame.Runtime.CharacterGeometry;

namespace UltramanGame.Runtime
{
    // Shapes redrawn from the Bing reference photographs listed in docs/角色造型参考.md.
    // The photographs are not runtime textures. All silhouettes are real, articulated meshes.
    public sealed class CharacterModel
    {
        readonly bool monster;
        readonly Material silver,red,purple,gold,eye,cyan,skin,armor,bone,mouth;
        readonly Vector3 pivot=new Vector3(0,1.24f,0);
        public readonly Transform Head;
        public readonly Transform[] Arms=new Transform[4],Legs=new Transform[4],Hands=new Transform[2],Feet=new Transform[2],Elbows=new Transform[2],Knees=new Transform[2];
        Mesh tailMesh;Vector3[] tailVertices,tailNormals;
        static Transform Group(Transform parent,string name,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static Material Surface(string name,Color color,float metal,float gloss)
        {var m=PrototypeActor.Material(color,metal);m.name=name;m.SetFloat("_Glossiness",gloss);return m;}
        public CharacterModel(Transform root,Transform torso,bool monster)
        {
            this.monster=monster;
            silver=Surface("Satin silver",new Color(.68f,.73f,.79f),.62f,.62f);
            red=Surface("Tiga crimson",new Color(.62f,.018f,.05f),.20f,.43f);
            purple=Surface("Tiga violet",new Color(.16f,.075f,.36f),.25f,.48f);
            gold=Surface("Chest gold inlay",new Color(.84f,.52f,.13f),.60f,.55f);
            eye=PrototypeActor.Material(new Color(1,.93f,.72f),.05f,true);eye.name="Luminous eyes";
            cyan=PrototypeActor.Material(new Color(.08f,.66f,1),.15f,true);cyan.name="Color timer";
            skin=Surface("Golza stone skin",new Color(.38f,.39f,.40f),.03f,.26f);
            var texture=Resources.Load<Texture2D>("Art/GolzaSkin");if(texture)skin.mainTexture=texture;
            armor=Surface("Golza worn shell",new Color(.39f,.37f,.27f),.05f,.25f);
            bone=Surface("Ivory claws",new Color(.63f,.53f,.32f),.05f,.32f);
            mouth=Surface("Golza maroon hide",new Color(.70f,.22f,.19f),.02f,.20f);
            if(texture) {armor.mainTexture=texture;armor.color=new Color(.94f,.88f,.66f);mouth.mainTexture=texture;}
            var body=Group(torso,monster?"Golza body sculpture":"Tiga suit sculpture",-pivot);
            if(monster)BuildMonsterBody(body);else BuildHeroBody(body);
            Combine(body);
            Head=Group(torso,monster?"Golza head rig":"Tiga head rig",new Vector3(0,monster?2.51f:2.57f,monster?.04f:0)-pivot);
            if(monster)BuildMonsterHead(Head);else BuildHeroHead(Head);
            Combine(Head);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                for(int section=0;section<2;section++)
                {Arms[side*2+section]=Limb(torso,false,section);Legs[side*2+section]=Limb(root,true,section);}
                Elbows[side]=Ellipsoid(torso,"Elbow",Vector3.zero,Vector3.one*(monster?.125f:.085f),monster?skin:purple);
                Knees[side]=Ellipsoid(root,"Knee",Vector3.zero,new Vector3(monster?.16f:.103f,monster?.15f:.105f,monster?.16f:.113f),monster?skin:silver);
                Hands[side]=Group(torso,"Hand",Vector3.zero);BuildHand(Hands[side],sign);Combine(Hands[side]);
                Feet[side]=Group(root,"Foot",Vector3.zero);BuildFoot(Feet[side]);Combine(Feet[side]);
            }
            if(monster)
            {
                var path=new Vector3[25];var radii=new float[25];
                for(int i=0;i<25;i++){path[i]=TailPoint(i/24f,0);radii[i]=TailRadius(i/24f);}
                var tail=Tube(root,"Continuous flexible tail",path,radii,skin,20);
                tailMesh=tail.GetComponent<MeshFilter>().sharedMesh;tailMesh.MarkDynamic();tailVertices=tailMesh.vertices;tailNormals=tailMesh.normals;
            }
        }
        void BuildHeroBody(Transform body)
        {
            // Waist-to-shoulder taper; symmetric purple flanks and red center.
            Loft(body,"Sculpted suit torso",new[]{new Ring(1.15f,.17f,.15f),new Ring(1.30f,.25f,.175f),new Ring(1.47f,.245f,.155f),new Ring(1.68f,.29f,.19f),new Ring(1.92f,.395f,.235f),new Ring(2.08f,.38f,.195f),new Ring(2.18f,.20f,.12f),new Ring(2.19f,.005f,.005f)},new[]{red,purple},(t,a)=>Mathf.Abs(Mathf.Sin(a))>.7f?1:0);
            Loft(body,"Red neck",new[]{new Ring(2.12f,.11f,.11f),new Ring(2.29f,.105f,.10f)},new[]{red});
            // Silver sweep over the stomach. Each side is a convex beveled panel.
            for(int side=0;side<2;side++)
            {
                float s=side==0?-1:1;
                HeroPlate(body,"Silver abdominal sweep",s,new[]{new Vector3(.015f,1.52f,.173f),new Vector3(.29f,1.82f,.186f),new Vector3(.36f,1.98f,.145f),new Vector3(.18f,1.88f,.239f),new Vector3(.015f,1.66f,.205f)},silver,.014f);
                Ellipsoid(body,"Deltoid",new Vector3(s*.43f,2.065f,0),new Vector3(.145f,.155f,.157f),purple);
                var pad=Ellipsoid(body,"Silver shoulder cap",new Vector3(s*.445f,2.12f,.0f),new Vector3(.151f,.113f,.162f),silver);pad.localRotation=Quaternion.Euler(0,0,s*-24);
                // Four nested curved strips continue around the upper chest.
                for(int back=0;back<2;back++)
                {
                    float z=back==0?1:-1;
                    for(int strip=0;strip<4;strip++)
                    {
                        float dy=strip*.029f;
                        Vector3[] points={new Vector3(s*.06f,1.935f+dy,z*.244f),new Vector3(s*.20f,1.975f+dy,z*.233f),new Vector3(s*.32f,2.02f+dy,z*.18f),new Vector3(s*.38f,2.09f+dy*.5f,z*.085f)};
                        Ribbon(body,"Curved chest inlay",points,.032f,strip%2==0?silver:gold);
                    }
                }
                HeroPlate(body,"Upper chest violet",s,new[]{new Vector3(.035f,1.97f,.24f),new Vector3(.31f,2.11f,.17f),new Vector3(.25f,2.15f,.16f),new Vector3(.06f,2.15f,.21f)},purple,.006f);
            }
            Ellipsoid(body,"Timer silver socket",new Vector3(0,2.075f,.233f),new Vector3(.09f,.112f,.045f),silver);
            Ellipsoid(body,"Timer gold rim",new Vector3(0,2.075f,.267f),new Vector3(.066f,.085f,.025f),gold);
            Ellipsoid(body,"Blue color timer",new Vector3(0,2.075f,.287f),new Vector3(.05f,.066f,.021f),cyan);
        }
        static void HeroPlate(Transform parent,string name,float sign,Vector3[] outline,Material mat,float depth)
        {
            for(int i=0;i<outline.Length;i++)outline[i].x*=sign;
            // Normalize the front face winding after mirroring.
            float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var b=outline[(i+1)%outline.Length];area+=a.x*b.y-b.x*a.y;}
            if(area<0)System.Array.Reverse(outline);Plate(parent,name,outline,depth,mat);
        }
        void BuildHeroHead(Transform head)
        {
            Loft(head,"Sculpted silver mask",new[]{new Ring(-.30f,.035f,.08f,.035f),new Ring(-.255f,.125f,.14f,.02f),new Ring(-.16f,.194f,.19f),new Ring(.02f,.222f,.205f),new Ring(.18f,.197f,.182f,-.015f),new Ring(.29f,.125f,.12f,-.035f),new Ring(.34f,.076f,.12f,-.045f),new Ring(.405f,.016f,.07f,-.045f),new Ring(.44f,.001f,.002f,-.075f)},new[]{silver},null,0,64,4);
            Tube(head,"Central mask ridge",new[]{new Vector3(0,.17f,.193f),new Vector3(0,.29f,.10f),new Vector3(0,.395f,.02f),new Vector3(0,.438f,-.07f)},new[]{.016f,.020f,.011f,.001f},silver);
            for(int side=0;side<2;side++)
            {
                float s=side==0?-1:1;
                var socket=Ellipsoid(head,"Eye socket",new Vector3(s*.119f,.015f,.185f),new Vector3(.094f,.062f,.027f),silver);socket.localRotation=Quaternion.Euler(0,s*20,s*19);
                var lens=Ellipsoid(head,"Almond eye lens",new Vector3(s*.122f,.019f,.202f),new Vector3(.080f,.050f,.024f),eye);lens.localRotation=socket.localRotation;
                HeroPlate(head,"Raised cheek plane",s,new[]{new Vector3(.055f,-.215f,.153f),new Vector3(.157f,-.20f,.139f),new Vector3(.195f,-.04f,.108f),new Vector3(.11f,-.10f,.193f)},silver,.012f);
                HeroPlate(head,"Brow ridge",s,new[]{new Vector3(.038f,.07f,.21f),new Vector3(.182f,.09f,.14f),new Vector3(.16f,.18f,.12f),new Vector3(.08f,.12f,.19f)},silver,.015f);
                Ellipsoid(head,"Ear inset",new Vector3(s*.216f,-.01f,-.022f),new Vector3(.021f,.069f,.051f),purple);
                Ellipsoid(head,"Ear rim",new Vector3(s*.227f,-.006f,-.024f),new Vector3(.012f,.046f,.028f),silver);
            }
            HeroPlate(head,"Forehead crystal border",1,new[]{new Vector3(0,.085f,.206f),new Vector3(.039f,.15f,.196f),new Vector3(0,.216f,.167f),new Vector3(-.039f,.15f,.196f)},silver,.014f);
            HeroPlate(head,"Forehead crystal",1,new[]{new Vector3(0,.104f,.225f),new Vector3(.022f,.15f,.216f),new Vector3(0,.195f,.194f),new Vector3(-.022f,.15f,.216f)},purple,.007f);
            HeroPlate(head,"Nose bridge",1,new[]{new Vector3(-.021f,-.09f,.20f),new Vector3(.021f,-.09f,.20f),new Vector3(.012f,.071f,.217f),new Vector3(-.012f,.071f,.217f)},silver,.022f);
            Ellipsoid(head,"Mouth recess",new Vector3(0,-.166f,.185f),new Vector3(.068f,.014f,.015f),purple);
            Tube(head,"Upper lip",new[]{new Vector3(-.067f,-.158f,.184f),new Vector3(0,-.148f,.199f),new Vector3(.067f,-.158f,.184f)},new[]{.008f,.01f,.008f},silver);
            Tube(head,"Lower lip",new[]{new Vector3(-.052f,-.179f,.18f),new Vector3(0,-.184f,.197f),new Vector3(.052f,-.179f,.18f)},new[]{.007f,.008f,.007f},silver);
        }
        void BuildMonsterBody(Transform body)
        {
            Loft(body,"Heavy kaiju torso",new[]{new Ring(.96f,.12f,.16f),new Ring(1.16f,.32f,.25f),new Ring(1.43f,.39f,.30f),new Ring(1.70f,.44f,.31f),new Ring(1.99f,.53f,.30f),new Ring(2.17f,.39f,.23f),new Ring(2.27f,.21f,.18f)},new[]{skin},null,.14f);
            Ellipsoid(body,"Maroon belly surround",new Vector3(0,1.48f,.245f),new Vector3(.27f,.48f,.10f),mouth,.2f);
            for(int i=0;i<7;i++)
            {
                float y=1.12f+i*.098f,w=.14f+Mathf.Sin(i/6f*Mathf.PI)*.08f;
                Ellipsoid(body,"Segmented belly scute",new Vector3(0,y,.325f),new Vector3(w,.078f,.052f),armor,.28f);
            }
            for(int side=0;side<2;side++)
            {
                float s=side==0?-1:1;
                var pec=Ellipsoid(body,"Massive pectoral armor",new Vector3(s*.26f,1.99f,.23f),new Vector3(.25f,.30f,.135f),skin,.12f);pec.localRotation=Quaternion.Euler(0,s*12,s*-17);
                Ellipsoid(body,"Rock shoulder",new Vector3(s*.56f,2.07f,0),new Vector3(.245f,.22f,.24f),skin,.2f);
            }
            for(int i=0;i<8;i++)
            {
                float y=1.16f+i*.125f;
                Tube(body,"Dorsal ridged spine",new[]{new Vector3(-.04f,y,-.27f),new Vector3(0,y+.075f,-.42f),new Vector3(.04f,y+.11f,-.27f)},new[]{.055f,.075f,.02f},mouth);
            }
        }
        void BuildMonsterHead(Transform head)
        {
            Ellipsoid(head,"Long reptile skull",new Vector3(0,.02f,.02f),new Vector3(.265f,.32f,.25f),mouth,.16f);
            // Layered helmet plates encircle the back, cheeks and throat, leaving the face open.
            for(int layer=0;layer<5;layer++)
            {
                float y=-.36f+layer*.12f,rx=.36f-layer*.023f;
                var path=new Vector3[21];var radii=new float[21];
                for(int j=0;j<path.Length;j++)
                {float a=(50+j*260f/20)*Mathf.Deg2Rad;path[j]=new Vector3(Mathf.Sin(a)*rx,y+Mathf.Cos(a)*.08f,Mathf.Cos(a)*(.23f+layer*.008f)-.01f);radii[j]=.09f;}
                Tube(head,"Layered neck shell",path,radii,armor,12);
            }
            HeroPlate(head,"Throat shield",1,new[]{new Vector3(0,-.47f,.16f),new Vector3(.33f,-.26f,.22f),new Vector3(.27f,-.13f,.20f),new Vector3(0,-.29f,.30f),new Vector3(-.27f,-.13f,.20f),new Vector3(-.33f,-.26f,.22f)},armor,.025f);
            HeroPlate(head,"Pointed forehead carapace",1,new[]{new Vector3(-.24f,.22f,.13f),new Vector3(0,.42f,.31f),new Vector3(.24f,.22f,.13f),new Vector3(.17f,.30f,.02f),new Vector3(-.17f,.30f,.02f)},armor,.075f);
            Ellipsoid(head,"Dark mouth opening",new Vector3(0,-.153f,.261f),new Vector3(.216f,.095f,.102f),mouth);
            Ellipsoid(head,"Broad flattened muzzle",new Vector3(0,-.033f,.248f),new Vector3(.235f,.095f,.174f),mouth,.15f);
            Ellipsoid(head,"Lower jaw",new Vector3(0,-.235f,.237f),new Vector3(.195f,.05f,.119f),mouth,.15f);
            for(int side=0;side<2;side++)
            {
                float s=side==0?-1:1;
                var eyeRim=Ellipsoid(head,"Eye brow",new Vector3(s*.192f,.092f,.22f),new Vector3(.061f,.086f,.044f),mouth);eyeRim.localRotation=Quaternion.Euler(0,s*25,s*-20);
                var iris=Ellipsoid(head,"Amber eye",new Vector3(s*.207f,.09f,.247f),new Vector3(.025f,.045f,.018f),gold);iris.localRotation=eyeRim.localRotation;
                Ellipsoid(head,"Vertical pupil",new Vector3(s*.21f,.09f,.263f),new Vector3(.008f,.032f,.009f),mouth);
                Ellipsoid(head,"Nostril",new Vector3(s*.081f,-.029f,.411f),new Vector3(.018f,.009f,.009f),skin);
                Tube(head,"Side fang",new[]{new Vector3(s*.20f,-.085f,.295f),new Vector3(s*.192f,-.13f,.34f),new Vector3(s*.178f,-.18f,.342f)},new[]{.03f,.018f,.001f},bone);
                for(int tooth=0;tooth<5;tooth++)
                {
                    float x=s*(.027f+tooth*.028f);
                    Tube(head,"Small lower tooth",new[]{new Vector3(x,-.209f,.332f),new Vector3(x,-.180f,.337f)},new[]{.012f,.001f},bone,8);
                }
                for(int i=0;i<12;i++)
                {
                    float a=i*1.7f;var p=new Vector3(s*(.27f+.065f*Mathf.Sin(a)),-.29f+(i%4)*.125f,.10f+.08f*Mathf.Cos(a));
                    Ellipsoid(head,"Carapace pockmark",p,new Vector3(.022f,.029f,.013f),skin,.1f);
                }
            }
        }
        Transform Limb(Transform parent,bool leg,int section)
        {
            var group=Group(parent,(leg?"Leg":"Arm")+" segment "+section,Vector3.zero);
            float radius=leg?(section==0?.159f:.116f):(section==0?.12f:.103f);
            if(monster)radius*=leg?1.70f:1.48f;
            var profile=new[]{new Ring(-.5f,radius*.72f,radius*.72f),new Ring(-.33f,radius*.97f,radius*.96f),new Ring(-.10f,radius,radius),new Ring(.20f,radius*.86f,radius*.85f),new Ring(.43f,radius*.64f,radius*.66f),new Ring(.5f,radius*.57f,radius*.59f)};
            if(monster)
            {
                Loft(group,"Rugged limb",profile,new[]{skin},null,.22f,32,4);
                for(int i=0;i<(leg?10:6);i++)
                {
                    float a=i*2.399f,y=-.34f+(i%4)*.20f;
                    var p=new Vector3(Mathf.Sin(a)*radius*.86f,y,Mathf.Cos(a)*radius*.86f);
                    var stud=Ellipsoid(group,"Worn pebble scale",p,new Vector3(radius*.21f,.05f,radius*.15f),i%3==0?armor:skin,.3f);stud.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
                }
            }
            else
            {
                Loft(group,"Contoured suit limb",profile,new[]{silver,red,purple},(t,a)=>{
                    float front=Mathf.Cos(a);float sweep=leg?(.4f+.24f*Mathf.Sin(t*Mathf.PI)):(.40f+.25f*Mathf.Sin(t*Mathf.PI));
                    if((section==1&&t>.86f)||(section==0&&t>.92f))return 0;
                    if(front>sweep+.13f)return 1;if(front>sweep-.10f)return 0;return 2;
                },0,96,8);
            }
            Combine(group);return group;
        }
        void BuildHand(Transform hand,float sign)
        {
            Ellipsoid(hand,"Palm",Vector3.zero,new Vector3(monster?.16f:.102f,monster?.14f:.095f,monster?.17f:.10f),monster?skin:silver,.03f);
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*(monster?.068f:.044f);
                if(monster)
                    Tube(hand,"Curved talon",new[]{new Vector3(x,.055f,.08f),new Vector3(x,.08f,.21f),new Vector3(x,.04f,.29f),new Vector3(x,-.025f,.31f)},new[]{.042f,.035f,.022f,.001f},bone);
                else
                    Ellipsoid(hand,"Curled finger knuckle",new Vector3(x,.025f,.071f),new Vector3(.027f,.069f,.047f),silver);
            }
            var thumb=Ellipsoid(hand,"Thumb",new Vector3(sign*.099f,-.04f,.026f),new Vector3(.04f,.066f,.046f),monster?skin:silver);thumb.localRotation=Quaternion.Euler(0,0,sign*-30);
        }
        void BuildFoot(Transform foot)
        {
            Ellipsoid(foot,"Shaped boot",Vector3.zero,new Vector3(monster?.185f:.117f,.105f,monster?.25f:.19f),monster?skin:silver,monster?.16f:0);
            if(monster)
                for(int i=0;i<4;i++)Tube(foot,"Toe claw",new[]{new Vector3((i-1.5f)*.083f,0,.14f),new Vector3((i-1.5f)*.087f,-.025f,.26f),new Vector3((i-1.5f)*.089f,-.07f,.32f)},new[]{.045f,.032f,.001f},bone);
            else
                Tube(foot,"Boot instep seam",new[]{new Vector3(-.082f,.057f,.07f),new Vector3(0,.105f,.03f),new Vector3(.082f,.057f,.07f)},new[]{.007f,.007f,.007f},purple,8);
        }
        public void AnimateTail(float time)
        {
            if(!monster)return;
            for(int r=0;r<25;r++)
            {
                float t=r/24f;Vector3 center=TailPoint(t,time);
                var rotation=Quaternion.FromToRotation(Vector3.up,TailPoint(Mathf.Min(1,t+.02f),time)-TailPoint(Mathf.Max(0,t-.02f),time));
                for(int side=0;side<=20;side++)
                {
                    float a=side*2*Mathf.PI/20;int index=r*21+side;
                    Vector3 normal=rotation*new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                    tailVertices[index]=center+normal*TailRadius(t);tailNormals[index]=normal;
                }
            }
            tailMesh.vertices=tailVertices;tailMesh.normals=tailNormals;tailMesh.RecalculateBounds();
        }
        static float TailRadius(float t) => .245f*Mathf.Pow(1-t,1.1f)+.002f;
        static Vector3 TailPoint(float t,float time)
        {return new Vector3(Mathf.Sin(time*1.4f-t*2)*.30f*t,1.03f-.72f*Mathf.Sin(t*Mathf.PI),-.14f-t*1.85f);}
    }
}
