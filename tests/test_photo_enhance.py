import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import numpy as np
import cv2
from vision.photo_enhance import configuration,parse_recipe,harmonise,enhance,local_fallback,make_prompt,MODEL

class PhotoEnhancementTests(unittest.TestCase):
    def recipe(self):
        return dict(exposure_ev=-.2,red_gain=.95,green_gain=1.,blue_gain=1.08,
                    saturation=.95,edge_feather_px=1.2,light_wrap=.1,shadow_strength=.15)

    def test_reads_only_explicit_config_and_never_executes_script(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'Downloads').mkdir();(root/'.claude').mkdir()
            (root/'Downloads/hlclaude 2').write_text('HUALAI_BASE_URL="https://example.invalid"\nexit 10\n')
            with self.assertRaisesRegex(ValueError,'credential_missing'):configuration(root)
            (root/'.claude/.env').write_text('UNRELATED=ignored\nLITELLM_API_KEY="unit-test-only"\n')
            self.assertEqual(('https://example.invalid','unit-test-only'),configuration(root))

    def test_model_output_is_bounded_data_and_cannot_execute_code(self):
        data=self.recipe();data['exposure_ev']=100;data['light_wrap']=-2
        recipe=parse_recipe(json.dumps(data));self.assertEqual(.15,recipe['exposure_ev']);self.assertEqual(0,recipe['light_wrap'])
        data['red_gain']=float('nan')
        with self.assertRaises(ValueError):parse_recipe(json.dumps(data))
        with self.assertRaises(ValueError):parse_recipe('__import__("os").system("echo test")')
        self.assertEqual('gpt-5.6-terra',MODEL)
        self.assertIn('2560x1440',make_prompt(2560,1440))

    def test_harmonisation_preserves_left_hero_and_background_geometry(self):
        plate=np.full((120,220,3),(40,35,30),np.uint8);mask=np.zeros((120,220),np.uint8)
        mask[25:107,140:190]=255;image=plate.copy();image[mask>0]=(90,120,170)
        result=harmonise(image,plate,mask,self.recipe())
        np.testing.assert_array_equal(result[:,:110],image[:,:110])
        self.assertEqual(image.shape,result.shape)
        self.assertLess(result[60,165,2],image[60,165,2])
        self.assertGreater(result[60,165,2],100)

    @staticmethod
    def linear(image):
        value=image.astype(np.float64)/255
        return np.where(value<=.04045,value/12.92,((value+.055)/1.055)**2.4)

    @staticmethod
    def encoded(image):
        value=np.clip(image,0,1)
        return np.uint8(np.rint(np.where(value<=.0031308,12.92*value,1.055*value**(1/2.4)-.055)*255))

    def test_exposure_and_translucent_edges_use_unity_linear_light(self):
        # A bright person against a dark blue plate with several broad alpha
        # bands. Compare the interior of each band with an independent physical
        # exposure/composite, not the implementation's own reconstruction.
        plate=np.full((120,320,3),(68,27,12),np.uint8)
        foreground=np.full_like(plate,(205,151,108))
        mask=np.zeros((120,320),np.uint8)
        for start,alpha in ((160,64),(200,128),(240,192),(280,255)):
            mask[:,start:start+40]=alpha
        a=mask[:,:,None]/255
        image=self.encoded(self.linear(foreground)*a+self.linear(plate)*(1-a))
        recipe=dict(exposure_ev=-.45,red_gain=1,green_gain=1,blue_gain=1,
                    saturation=1,edge_feather_px=.6,light_wrap=0,shadow_strength=0)
        result=harmonise(image,plate,mask,recipe)
        expected=self.encoded(self.linear(foreground)*2**(-.45)*a+self.linear(plate)*(1-a))
        for x in (180,220,260,300):
            self.assertLessEqual(np.abs(result[60,x].astype(int)-expected[60,x]).max(),1)
        np.testing.assert_array_equal(image[:,:150],result[:,:150])

    def test_ground_contact_stays_under_feet_and_skips_cropped_portrait(self):
        recipe=dict(exposure_ev=0,red_gain=1,green_gain=1,blue_gain=1,
                    saturation=1,edge_feather_px=.6,light_wrap=0,shadow_strength=.24)
        plate=np.full((360,640,3),100,np.uint8)
        mask=np.zeros((360,640),np.uint8)
        mask[110:260,380:570]=255
        mask[240:328,385:420]=255;mask[240:328,530:565]=255
        image=plate.copy();image[mask>0]=170
        result=harmonise(image,plate,mask,recipe)
        self.assertLess(int(result[330,400,0]),100)
        self.assertLess(int(result[330,545,0]),100)
        self.assertEqual(100,int(result[330,475,0]),'no broad stripe bridging the legs')
        np.testing.assert_array_equal(image[:,:320],result[:,:320])
        # A portrait ending at the image edge has no proven visible feet.
        mask[200:,380:570]=255;image=plate.copy();image[mask>0]=170
        result=harmonise(image,plate,mask,recipe)
        np.testing.assert_array_equal(image[mask==0],result[mask==0])

    def test_camera_frame_crop_gets_a_short_edge_falloff(self):
        recipe=dict(exposure_ev=0,red_gain=1,green_gain=1,blue_gain=1,
                    saturation=1,edge_feather_px=.6,light_wrap=.2,shadow_strength=0)
        plate=np.full((120,180,3),(34,54,82),np.uint8)
        mask=np.zeros((120,180),np.uint8);mask[24:108,:42]=255
        image=plate.copy();image[mask>0]=(180,116,72)
        result=harmonise(image,plate,mask,recipe)
        # The subject genuinely touches the left camera edge. Its interior is
        # retained, but the first pixel is allowed to meet the new background
        # without a one-pixel-wide vertical cut line.
        self.assertLess(int(result[62,0,0]),int(result[62,18,0]))
        self.assertGreater(int(result[62,26,0]),int(plate[62,26,0]))
        np.testing.assert_array_equal(result[:,90:],image[:,90:])

    def test_original_survives_success_or_model_failure(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);source=root/'photo.png';plate=root/'plate.png';mask=root/'mask.png'
            image=np.full((64,96,3),90,np.uint8);cv2.imwrite(str(source),image);cv2.imwrite(str(plate),image)
            cv2.imwrite(str(mask),np.zeros((64,96),np.uint8));original=source.read_bytes()
            with patch('vision.photo_enhance.configuration',return_value=('https://example.invalid','test-only')):
                with patch('vision.photo_enhance.ask_model',side_effect=ValueError('invalid')):
                    with self.assertRaises(ValueError):enhance(source,plate,mask)
                    self.assertFalse((root/'photo_AI.png').exists())
                with patch('vision.photo_enhance.ask_model',return_value=self.recipe()):
                    output,_=enhance(source,plate,mask);self.assertTrue(output.is_file())
            self.assertEqual(original,source.read_bytes())

    def test_near_zero_model_recipe_still_produces_visible_ai_variant(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);source=root/'photo.png';plate=root/'plate.png';mask=root/'mask.png'
            image=np.full((120,180,3),(95,115,145),np.uint8)
            clean=image.copy();clean[25:110,110:165]=(180,145,115)
            matte=np.zeros((120,180),np.uint8);matte[25:110,110:165]=255
            cv2.imwrite(str(source),clean);cv2.imwrite(str(plate),image);cv2.imwrite(str(mask),matte)
            near_zero=self.recipe();near_zero.update(exposure_ev=0,red_gain=1,green_gain=1,blue_gain=1,
                saturation=1,edge_feather_px=.6,light_wrap=0,shadow_strength=0)
            with patch('vision.photo_enhance.configuration',return_value=('https://example.invalid','test-only')):
                with patch('vision.photo_enhance.ask_model',return_value=near_zero):
                    output,_=enhance(source,plate,mask)
            result=cv2.imread(str(output));delta=np.abs(result.astype(np.int16)-clean.astype(np.int16))
            self.assertGreater(float(delta.mean()),1.5)
            self.assertGreater(float(np.mean(np.any(delta>3,axis=2))),.20)

    def test_scene_response_is_visible_but_retains_person_detail(self):
        # Two different plate lights exercise the spatial response instead of
        # a flat global colour wash. The checkerboard inside the person is a
        # tiny stand-in for hair/clothing detail that must survive the pass.
        height,width=180,300
        plate=np.zeros((height,width,3),np.uint8)
        plate[:,:150]=(95,55,28);plate[:,150:]=(35,65,105)
        mask=np.zeros((height,width),np.uint8);mask[32:158,170:270]=255
        image=plate.copy()
        image[mask>0]=(150,120,90)
        for y in range(50,150,20):
            for x in range(180,260,20):
                if ((x//20)+(y//20))%2:
                    image[y:y+10,x:x+10]=(210,165,115)
        recipe=dict(exposure_ev=0,red_gain=1,green_gain=1,blue_gain=1,
                    saturation=1,edge_feather_px=1.0,light_wrap=.42,shadow_strength=0)
        result=harmonise(image,plate,mask,recipe)
        self.assertEqual(image.shape,result.shape)
        # The fixed left scene/hero side is byte-for-byte untouched.
        np.testing.assert_array_equal(image[:,:145],result[:,:145])
        person=(mask==255)
        delta=np.abs(result.astype(np.int16)-image.astype(np.int16))
        self.assertGreater(float(delta[person].mean()),4.0)
        self.assertGreater(float(np.mean(np.any(delta[person]>6,axis=1))),.75)
        # Distinct foreground detail remains distinct after environment light.
        dark=result[65,185].astype(int);light=result[55,185].astype(int)
        self.assertGreater(float(np.abs(light-dark).mean()),12.0)
        # The plate outside the narrow edge band remains unchanged.
        np.testing.assert_array_equal(image[:,0:145],result[:,0:145])

    def test_edge_feather_does_not_blur_subject_interior(self):
        # The matte boundary may borrow nearby colour, but the opaque centre
        # must retain high-frequency face/clothing detail from the source.
        height,width=160,240
        plate=np.full((height,width,3),(35,48,70),np.uint8)
        image=plate.copy();mask=np.zeros((height,width),np.uint8)
        mask[24:140,80:210]=255
        image[mask>0]=(150,110,78)
        for y in range(42,124,8):
            for x in range(96,194,8):
                if ((x//8)+(y//8))%2:image[y:y+4,x:x+4]=(220,180,132)
        recipe=dict(exposure_ev=0,red_gain=1,green_gain=1,blue_gain=1,
                    saturation=1,edge_feather_px=4,light_wrap=0,shadow_strength=0)
        result=harmonise(image,plate,mask,recipe)
        # The eroded centre is outside the feather ring and should be byte
        # stable (apart from no-op linear encode rounding, which is exact for
        # these authored values).
        np.testing.assert_array_equal(result[48:116,112:178],image[48:116,112:178])

    def test_local_fallback_creates_a_visible_but_bounded_variant(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);source=root/'photo.png';plate=root/'plate.png';mask=root/'mask.png'
            image=np.full((120,180,3),(95,115,145),np.uint8)
            clean=image.copy();clean[25:110,110:165]=(180,145,115)
            matte=np.zeros((120,180),np.uint8);matte[25:110,110:165]=255
            cv2.imwrite(str(source),clean);cv2.imwrite(str(plate),image);cv2.imwrite(str(mask),matte)
            output,recipe=local_fallback(source,plate,mask)
            result=cv2.imread(str(output));delta=np.abs(result.astype(np.int16)-clean.astype(np.int16))
            self.assertGreater(recipe['light_wrap'],0)
            self.assertGreater(np.count_nonzero(np.any(delta>2,axis=2)),100)
            self.assertGreater(float(delta.mean()),3.0,'local fallback must be visibly different from the source')
            self.assertGreater(float(np.mean(np.any(delta>2,axis=2))),.20,'fusion must affect a meaningful part of the person')
            np.testing.assert_array_equal(result[:,:100],clean[:,:100])

    def test_worker_reports_safe_transport_class_without_exception_details(self):
        import ssl
        import urllib.error
        from scripts.enhance_photo import main
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);status=root/'status.json'
            args=['enhance_photo','--input',str(root/'input.png'),'--plate',str(root/'plate.png'),
                  '--mask',str(root/'mask.png'),'--status',str(status)]
            error=urllib.error.URLError(ssl.SSLEOFError('private request details must not appear'))
            with patch('sys.argv',args),patch('scripts.enhance_photo.enhance',side_effect=error):
                self.assertEqual(1,main())
            result=json.loads(status.read_text())
            self.assertFalse(result['ok']);self.assertEqual('SSLEOFError',result['error_type'])
            self.assertNotIn('private request',status.read_text())
            # urllib also accepts a string reason, with no traceback object.
            with patch('sys.argv',args),patch('scripts.enhance_photo.enhance',side_effect=urllib.error.URLError('private text reason')):
                self.assertEqual(1,main())
            self.assertFalse(json.loads(status.read_text())['ok'])
            self.assertNotIn('private text',status.read_text())
