import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import numpy as np
import cv2
from vision.photo_enhance import configuration,parse_recipe,harmonise,enhance,make_prompt,MODEL

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
