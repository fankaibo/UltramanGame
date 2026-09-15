"""Convert publicly downloaded Source heroes and retarget the authored Tiga clips.
Run in Blender with --disable-autoexec. Source archives remain untouched.
"""
import argparse, math, os, re, sys, json
from pathlib import Path
import bpy
from mathutils import Matrix, Vector


def import_hero(name, source, output):
    os.environ['NO_BPY']='1'
    sys.path.insert(0,str(Path('.cache/character-tools').resolve()))
    from SourceIO.blender_bindings.models import import_model
    from SourceIO.blender_bindings.models.common import put_into_collections
    from SourceIO.blender_bindings.operators.import_settings_base import ModelOptions
    from SourceIO.library.shared.content_manager import ContentManager
    from SourceIO.library.shared.content_manager.providers.loose_files import LooseFilesContentProvider
    from SourceIO.library.utils import FileBuffer
    from SourceIO.library.utils.tiny_path import TinyPath
    from SourceIO.library.source1.vtf import load_texture
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    manager=ContentManager();manager.add_child(LooseFilesContentProvider(TinyPath(str(source.resolve()))))
    options=ModelOptions.default();options.import_textures=options.import_animations=options.create_flex_drivers=False
    mdl=TinyPath(str(next(source.rglob('*human_size.mdl')).resolve()))
    with FileBuffer(mdl) as stream: container=import_model(mdl,stream,manager,options)
    put_into_collections(container,name)
    rig=container.armature;rig.name=name+'Rig'
    meshes=list(container.objects)
    if name=='Zero':
        for obj in list(meshes):
            if 'Armor' in obj.name or 'Bracelet' in obj.name:
                meshes.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
    files={str(p.relative_to(source)).lower():p for p in source.rglob('*') if p.is_file()}
    texture_dir=output/'Textures';texture_dir.mkdir(parents=True,exist_ok=True)
    material_cache={}
    for obj in meshes:
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00001);bpy.ops.object.mode_set(mode='OBJECT')
        for poly in obj.data.polygons:poly.use_smooth=True
        # Smooth the original silhouette while preserving the character's actual mesh and UVs.
        sub=obj.modifiers.new('Silhouette smoothing','SUBSURF');sub.levels=sub.render_levels=1
        for slot in obj.material_slots:
            original=slot.material.name.split('.')[0]
            if original in material_cache:slot.material=material_cache[original];continue
            vmt=next((p for k,p in files.items() if k.endswith('/'+original.lower()+'.vmt')),None)
            if not vmt:raise ValueError('Missing material '+original)
            text=vmt.read_text();match=re.search(r'"?\$basetexture"?\s+"([^"\n]+)"',text,re.I)
            if not match:raise ValueError('Missing basetexture '+original)
            path=files['materials/'+match[1].replace('\\','/').lower()+'.vtf']
            with FileBuffer(TinyPath(str(path.resolve()))) as stream:pixels,h,w=load_texture(stream)
            key=name+'_'+original
            image=bpy.data.images.new(key,width=w,height=h,alpha=True);image.pixels.foreach_set(pixels[::-1].ravel());image.update()
            image.filepath_raw=str(texture_dir/(key+'.png'));image.file_format='PNG';image.save()
            mat=bpy.data.materials.new(key);mat.use_nodes=True
            shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Roughness'].default_value=.43;shader.inputs['Metallic'].default_value=.2
            tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
            if 'eye' in original.lower() or 'timer' in original.lower():
                mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Emission Color']);shader.inputs['Emission Strength'].default_value=.65
            slot.material=mat;material_cache[original]=mat
    return rig,meshes


def import_gta(name,source,output):
    sys.path.insert(0,str(Path('.cache/character-tools').resolve()))
    import DragonFF
    DragonFF.register()
    from DragonFF.ops.txd_importer import import_txd
    from DragonFF.ops.dff_importer import import_dff
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    tx=import_txd({'file_name':str(source/(name+'.txd')),'skip_mipmaps':True,'pack':True})
    import_dff(dict(file_name=str(source/(name+'.dff')),txd_images=tx.images,image_ext='PNG',connect_bones=False,use_mat_split=False,remove_doubles=True,create_backfaces=False,group_materials=True,import_normals=True,materials_naming='TEX'))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.name=name+'Rig'
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    # DragonFF parents the armature to the mesh in these GTA skin files.
    # Detach before normalizing: assigning both world matrices with that parent
    # intact applies the transform twice after dependency-graph evaluation.
    bpy.context.view_layer.update()
    objs=[rig]+meshes;original={o:o.matrix_world.copy() for o in objs}
    for obj in objs:obj.parent=None;obj.matrix_parent_inverse=Matrix.Identity(4)
    transform=Matrix.Scale(.75,4)@Matrix.Translation(Vector((0,0,1.02)))@Matrix.Rotation(-math.pi/2,4,'Z')
    for obj in objs:obj.matrix_world=transform@original[obj]
    bpy.context.view_layer.update()
    for obj in objs:
        if any(abs(obj.matrix_world[i][j]-(transform@original[obj])[i][j])>.0001 for i in range(4) for j in range(4)):
            raise ValueError('Character world transform changed after evaluation: '+obj.name)
    names={'Normal':'root','Pelvis':'bip_pelvis','Spine':'bip_spine_0','Spine1':'bip_spine_1','Neck':'bip_neck','Head':'bip_head'}
    for side in ['L','R']:
        for a,b in [('UpperArm','upperArm'),('ForeArm','lowerArm'),('Hand','hand'),('Thigh','hip'),('Calf','knee'),('Foot','foot'),('Toe0','toe')]:names[f'{side} {a}']=f'bip_{b}_{side}'
        names[f'Bip01 {side} Clavicle']='bip_collar_'+side
        names[f'{side} Finger']='bip_middle_0_'+side;names[f'{side} Finger01']='bip_middle_1_'+side
    for b in list(rig.data.bones):
        new=names.get(b.name.strip())
        if not new:continue
        old=b.name;b.name=new
        for o in meshes:
            if old in o.vertex_groups:o.vertex_groups[old].name=new
    texture_dir=output/'Textures';texture_dir.mkdir(parents=True,exist_ok=True)
    for o in meshes:
        for p in o.data.polygons:p.use_smooth=True
        sub=o.modifiers.new('Silhouette smoothing','SUBSURF');sub.levels=sub.render_levels=1
        for slot in o.material_slots:
            mat=slot.material
            tex=next(n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
            key=name+'_'+tex.name.split('/')[-2];tex.filepath_raw=str(texture_dir/(key+'.png'));tex.file_format='PNG';tex.save()
            mat.name=key
            sh=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');sh.inputs['Metallic'].default_value=.2;sh.inputs['Roughness'].default_value=.43
    return rig,meshes


def retarget(rig):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(Path('unity/Assets/Resources/Characters/Tiga/Tiga.fbx').resolve()))
    imported=set(bpy.data.objects)-before;target=next(o for o in imported if o.type=='ARMATURE')
    target_actions=[a for a in bpy.data.actions if 'Armature.001|' in a.name]
    mapping={'bip_pelvis':'hip','bip_spine_0':'spineLower','bip_spine_1':'spineUpper','bip_spine_2':'spineChest','bip_neck':'neckLower','bip_head':'head'}
    for side in ['L','R']:
        for a,b in [('collar','Collar'),('upperArm','armBase'),('lowerArm','ForearmBase'),('hand','HandBase'),('hip','ThighBase'),('knee','Shin'),('foot','Foot'),('toe','toes')]:mapping[f'bip_{a}_{side}']=f'{b}_{side}'
        for finger,prefix in [('index','Index'),('middle','Mid'),('ring','Ring'),('pinky','Pinky'),('thumb','Thumb')]:
            for i in range(3):mapping[f'bip_{finger}_{i}_{side}']=f'{prefix}{i+1}_{side}'
    rest={b.name:(rig.matrix_world@b.matrix_local).copy() for b in rig.data.bones}
    target_rest={b.name:(target.matrix_world@b.matrix_local).copy() for b in target.data.bones}
    ratio=rest['bip_pelvis'].translation.z/target_rest['hip'].translation.z
    segments={}
    for side in ['L','R']:
        for a,b in [('collar','upperArm'),('upperArm','lowerArm'),('lowerArm','hand'),('hip','knee'),('knee','foot'),('foot','toe')]:segments[f'bip_{a}_{side}']=f'bip_{b}_{side}'
        segments['bip_hand_'+side]='bip_middle_0_'+side
        for finger in ['index','middle','ring','pinky','thumb']:
            for i in range(2):segments[f'bip_{finger}_{i}_{side}']=f'bip_{finger}_{i+1}_{side}'
    bones=list(rig.pose.bones);inv=rig.matrix_world.inverted();actions={}
    bpy.context.scene.render.fps=60
    rig.animation_data_create()
    for a in target_actions:
        name=a.name.split('|')[-1];action=bpy.data.actions.new(name);action.use_fake_user=True
        target.animation_data.action=a
        target.animation_data.use_nla=False
        if len(a.slots):target.animation_data.action_slot=a.slots[0]
        prev={}
        for frame in range(int(a.frame_range[0]),int(a.frame_range[1])+1):
            rig.animation_data.action=None;bpy.context.scene.frame_set(frame)
            for p in bones:p.matrix_basis=Matrix.Identity(4)
            bpy.context.view_layer.update()
            for p in bones:
                if p.name not in mapping:continue
                dest=mapping[p.name];tr=target_rest[dest];tp=target.matrix_world@target.pose.bones[dest].matrix
                rotation=(tp.to_quaternion()@tr.to_quaternion().inverted())@rest[p.name].to_quaternion()
                child=segments.get(p.name)
                if child and child in rest and child in mapping:
                    source_dir=rest[child].translation-rest[p.name].translation
                    target_dir=(target.matrix_world@target.pose.bones[mapping[child]].matrix).translation-tp.translation
                    if source_dir.length>.001 and target_dir.length>.001:
                        rotation=source_dir.rotation_difference(target_dir)@rest[p.name].to_quaternion()
                if p.name.startswith('bip_hand_'):
                    side=p.name[-1];idx='bip_index_0_'+side;pink='bip_pinky_0_'+side
                    if 'bip_middle_0_'+side in rest:
                        def basis(forward,across):
                            y=forward.normalized();x=(across-y*across.dot(y)).normalized();return Matrix((x,y,x.cross(y))).transposed()
                        # GTA skins have one two-joint finger group for the whole
                        # hand. Their T-pose thumbs face forward (-Y); preserve
                        # this palm roll instead of solving only finger direction.
                        across=rest[idx].translation-rest[pink].translation if idx in rest and pink in rest else Vector((0,-1,0))
                        src=basis(rest['bip_middle_0_'+side].translation-rest[p.name].translation,across)
                        dst=basis((target.matrix_world@target.pose.bones['Mid1_'+side].matrix).translation-tp.translation,(target.matrix_world@target.pose.bones['Index1_'+side].matrix).translation-(target.matrix_world@target.pose.bones['Pinky1_'+side].matrix).translation)
                        rotation=(dst@src.transposed()).to_quaternion()@rest[p.name].to_quaternion()
                if p.name=='bip_pelvis':pos=rest[p.name].translation+(tp.translation-tr.translation)*ratio
                elif p.parent:pos=(rig.matrix_world@p.parent.matrix)@(rest[p.parent.name].inverted()@rest[p.name].translation)
                else:pos=rest[p.name].translation
                p.rotation_mode='QUATERNION';p.matrix=inv@Matrix.LocRotScale(pos,rotation,rest[p.name].to_scale())
                bpy.context.view_layer.update()
            # Keep the source foot landmarks at their neutral floor height.
            floor=min((rig.matrix_world@rig.pose.bones['bip_foot_'+s].matrix).translation.z-rest['bip_foot_'+s].translation.z for s in ['L','R'])
            if floor<0:
                p=rig.pose.bones['bip_pelvis'];m=p.matrix.copy();m.translation+=inv.to_3x3()@Vector((0,0,-floor));p.matrix=m;bpy.context.view_layer.update()
            rig.animation_data.action=action
            for p in bones:
                p.rotation_mode='QUATERNION';q=p.rotation_quaternion.copy()
                if p.name in prev and q.dot(prev[p.name])<0:q.negate();p.rotation_quaternion=q
                prev[p.name]=q
                p.keyframe_insert('location',frame=frame,group=p.name);p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
            rig.animation_data.action=None
        for c in action.fcurves:
            for k in c.keyframe_points:k.interpolation='LINEAR'
        actions[name]=action
    for o in imported:bpy.data.objects.remove(o,do_unlink=True)
    for a in target_actions:bpy.data.actions.remove(a,do_unlink=True)
    rig.animation_data.action=actions['Idle'];bpy.context.scene.frame_set(1)
    return actions


def export(name,rig,meshes,actions,output):
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
    for o in meshes:o.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(output/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='RELATIVE',use_mesh_modifiers=True)
    bpy.ops.wm.save_as_mainfile(filepath=str((Path('artifacts/character-review')/(name+'-animated.blend')).resolve()))
    (output/'source.json').write_text(json.dumps({'character':name,'vertices':sum(len(o.data.vertices) for o in meshes),'clips':list(actions)},indent=2))


def review(name,rig,actions,output):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.render.resolution_x=800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('World');scene.world.use_nodes=True
    bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.16,.18,.23,1);bg.inputs[1].default_value=.5
    cam=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.85
    cam.location=(.9,-4,1.35);cam.rotation_euler=(Vector((0,0,.76))-cam.location).to_track_quat('-Z','Y').to_euler()
    for n,loc,power,color in [('Key',(2,-3,4),260,(1,.87,.75)),('Fill',(-2,-2,2),180,(.6,.75,1)),('Rim',(1,2,3),330,(.6,.8,1))]:
        d=bpy.data.lights.new(n,'AREA');d.energy=power;d.color=color;d.size=3;o=bpy.data.objects.new(n,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
    folder=Path('artifacts/character-review')/name;folder.mkdir(parents=True,exist_ok=True)
    for clip,f in [('Idle',1),('LeftPunch',8),('RightPunch',8),('Guard',20),('Beam',52),('Victory',48)]:
        rig.animation_data.action=actions[clip];scene.frame_set(f);scene.render.filepath=str((folder/(clip+'.png')).resolve());bpy.ops.render.render(write_still=True)
    # A fixed full-body portrait also supplies the selected hero for the photo stage.
    rig.animation_data.action=actions['Victory'];scene.frame_set(48);scene.render.filepath=str(output/'Photo.png');bpy.ops.render.render(write_still=True)
    from bpy_extras.object_utils import world_to_camera_view
    def point(bone):return (rig.matrix_world@rig.pose.bones[bone].matrix).translation
    shoulder=(point('bip_upperArm_L')+point('bip_upperArm_R'))/2
    crown=point('bip_head')+Vector((0,0,.14))
    a=world_to_camera_view(scene,cam,shoulder);b=world_to_camera_view(scene,cam,crown)
    (output/'PhotoMetrics.json').write_text(json.dumps({'center':a.x*800,'shoulder':a.y*1000,'crown':b.y*1000}))


def main():
    p=argparse.ArgumentParser();p.add_argument('name',choices=['Mebius','Zero','Geed','Grigio']);p.add_argument('source',type=Path);args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    output=Path('unity/Assets/Resources/Characters')/args.name;output.mkdir(parents=True,exist_ok=True);output=output.resolve()
    rig,meshes=(import_gta if args.name in ['Geed','Grigio'] else import_hero)(args.name,args.source,output);actions=retarget(rig);export(args.name,rig,meshes,actions,output);review(args.name,rig,actions,output)
if __name__=='__main__':main()
