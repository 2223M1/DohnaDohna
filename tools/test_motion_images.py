"""Geometry regression tests with synthetic pixels, no commercial assets required."""
import tempfile
import unittest
from pathlib import Path
from PIL import Image, ImageDraw
from motion_images import compose, contact, ground_shadow


class MotionImageTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)

    def tearDown(self):
        self.directory.cleanup()

    def sprite(self, width, name):
        picture = Image.new("RGBA", (width, 100))
        ImageDraw.Draw(picture).rectangle((width//2-10, 20, width//2+9, 99), fill="white")
        picture.save(self.root/name)
        return {"CgName":name,"PosX":0,"PosY":0,"IsShadow":1}

    def test_bottom_middle_origin_does_not_slide_when_image_width_changes(self):
        a, ao = compose(self.root, [self.sprite(120, "a.png")])
        b, bo = compose(self.root, [self.sprite(200, "b.png")])
        self.assertEqual(ao, (-10, -80))
        self.assertEqual(ao, bo)
        self.assertEqual(contact(a, ao), contact(b, bo))

    def test_center_origin_is_middle_middle_not_middle_right(self):
        layer = self.sprite(120, "a.png")
        layer["IsCenterOrigin"] = 1
        _, origin = compose(self.root, [layer])
        self.assertEqual(origin, (-10, -30))

    def test_asymmetric_feet_do_not_recenter_authored_station(self):
        picture = Image.new("RGBA", (180, 120))
        draw = ImageDraw.Draw(picture)
        draw.rectangle((10, 90, 35, 113), fill="white")
        draw.rectangle((135, 90, 160, 119), fill="white")
        self.assertEqual(contact(picture, (-90, -112)), [0, 8])
        # A different lower foot must not change the horizontal station either.
        self.assertEqual(contact(picture.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (-90, -112)), [0, 8])

    def test_authored_layer_offset_is_not_cancelled_by_calibration(self):
        layer = self.sprite(120, "a.png")
        layer["PosX"] = 42
        image, origin = compose(self.root, [layer])
        self.assertEqual(origin, (32, -80))
        self.assertEqual(contact(image, origin), [0, 0])

    def test_empty_idle_rejected(self):
        with self.assertRaisesRegex(ValueError, "no ground contact"):
            contact(Image.new("RGBA", (10, 10)), (0, 0))

    def test_shadow_ignores_elevation_but_keeps_horizontal_travel(self):
        layer = self.sprite(120, "a.png")
        layer.update(PosX=45, PosY=-180)
        _, body_origin = compose(self.root, [layer])
        _, shadow_origin = compose(self.root, [layer], shadow=True)
        self.assertEqual(body_origin, (35, -260))
        self.assertEqual(shadow_origin, (35, -80))

    def test_painted_shadow_uses_native_alpha_and_ground_plane(self):
        image, origin = compose(self.root, [self.sprite(120, "a.png")], shadow=True)
        shadow, point = ground_shadow(image, origin)
        self.assertEqual(point[0], origin[0]-8)
        self.assertLess(point[1], 0)
        self.assertGreater(point[1]+shadow.height, 0)
        self.assertLessEqual(shadow.getchannel("A").getextrema()[1], 101)
        self.assertGreater(shadow.getchannel("A").getextrema()[1], 80)
        self.assertEqual(shadow.getpixel((0,0))[3], 0)

    def test_painted_shadow_changes_with_pose_and_is_repeatable(self):
        image, origin = compose(self.root, [self.sprite(120, "a.png")], shadow=True)
        a, ao = ground_shadow(image, origin)
        b, bo = ground_shadow(image, origin)
        self.assertEqual(a.tobytes(), b.tobytes())
        self.assertEqual(ao, bo)
        wider = image.resize((image.width*2, image.height))
        c, _ = ground_shadow(wider, origin)
        self.assertGreater(c.width, a.width)


if __name__ == "__main__":
    unittest.main()
