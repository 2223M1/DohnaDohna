import io
from pathlib import Path
import struct
import tempfile
import unittest
import zlib

from extract_reference import AfaArchive, safe_relative, save_bytes, normalize_json_export, classify_image, remove_empty_subdirectories


class ArchiveTests(unittest.TestCase):
    def make_archive(self):
        name = '立绘／测试.qnt'.encode('gb18030')
        padded = (len(name) + 3) // 4 * 4
        toc = struct.pack('<II', len(name), padded) + name.ljust(padded, b'\0') + struct.pack('<IIII', 0, 0, 8, 10)
        compressed = zlib.compress(toc)
        offset = 256
        header = b'AFAH' + struct.pack('<I', 28) + b'AlicArch' + struct.pack('<III', 2, 1, offset)
        header += b'INFO' + struct.pack('<III', len(compressed) + 16, len(toc), 1)
        return (header + compressed).ljust(offset, b'\0') + b'DATAxxxx0123456789'

    def test_split_boundary_and_encoding(self):
        data = self.make_archive()
        with tempfile.TemporaryDirectory() as directory:
            parts = [Path(directory) / '001.bin', Path(directory) / '002.bin']
            parts[0].write_bytes(data[:269]); parts[1].write_bytes(data[269:])
            with AfaArchive(parts) as archive:
                self.assertEqual(archive.entries[0]['name'], '立绘／测试.qnt')
                self.assertEqual(archive.read(264, 10), b'0123456789')
                self.assertEqual(archive.read(269, 5), b'56789')
                with self.assertRaises(ValueError): archive.read(270, 10)

    def test_corrupt_archive_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'bad.afa'; path.write_bytes(self.make_archive()[:-1])
            with self.assertRaises(ValueError): AfaArchive([path])

    def test_safe_path(self):
        self.assertEqual(safe_relative('立绘／测试.png'), Path('立绘') / '测试.png')
        self.assertEqual(safe_relative('CON.png'), Path('_CON.png'))
        self.assertEqual(safe_relative('a:b.png'), Path('a%3Ab.png'))
        for name in ('../a', '/a', 'a/../b', 'a//b', 'a\\..\\b'):
            with self.assertRaises(ValueError): safe_relative(name)

    def test_no_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'out'
            save_bytes(path, b'one'); save_bytes(path, b'one')
            with self.assertRaises(FileExistsError): save_bytes(path, b'two')
            self.assertEqual(path.read_bytes(), b'one')

    def test_mixed_json_encoding_preserved(self):
        import json
        original = b'{"utf8":"' + '立绘'.encode('utf-8') + b'","name2":"' + '变量'.encode('gb18030') + b'"}'
        output, changes = normalize_json_export(original)
        self.assertEqual(json.loads(output), {'utf8': '立绘', 'name2': '变量'})
        self.assertEqual(len(changes), 1)
        self.assertIn('original_hex', changes[0])

    def test_image_classification(self):
        samples = {'立绘／人物／通常.qnt': '立绘', '系统／按钮.qnt': '界面与图标',
                   '背景／街道.ajp': '场景与背景', '特效／火／０１.ajp': '特效',
                   '事件／人物／场景.qnt': '剧情图像', '阿熊／攻击１／０１.ajp': '角色与敌人',
                   'LineEffect.qnt': '特效', 'unknown.qnt': '待确认'}
        for name, expected in samples.items():
            self.assertEqual(classify_image(name)[0], expected)
        self.assertEqual(classify_image('情感图标／人物.pcf')[0], '界面与图标')
        self.assertEqual(classify_image('人材ダウン／０１.ajp')[0], '角色与敌人')
        self.assertEqual(classify_image('ヒット特效／０１.ajp')[0], '特效')
        self.assertEqual(classify_image('原型机／右肩.qnt', {'原型机'})[0], '角色与敌人')

    def test_empty_cleanup_preserves_files_and_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'audio'
            (root / 'sound' / '迷宫').mkdir(parents=True)
            (root / 'unused' / 'nested').mkdir(parents=True)
            (root / 'music').mkdir()
            save_bytes(root / 'music' / 'track.ogg', b'unchanged audio')
            save_bytes(root / 'sound' / '.keep', b'keep hidden file')
            removed = remove_empty_subdirectories(root)
            self.assertEqual(set(removed), {root / 'sound' / '迷宫', root / 'unused' / 'nested', root / 'unused'})
            self.assertTrue(root.is_dir())
            self.assertEqual((root / 'music' / 'track.ogg').read_bytes(), b'unchanged audio')
            self.assertEqual((root / 'sound' / '.keep').read_bytes(), b'keep hidden file')
            self.assertEqual(remove_empty_subdirectories(root), [])

    def test_empty_cleanup_requires_existing_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(FileNotFoundError):
                remove_empty_subdirectories(root / 'missing')
            save_bytes(root / 'file', b'preserved')
            with self.assertRaises(NotADirectoryError):
                remove_empty_subdirectories(root / 'file')
            self.assertEqual((root / 'file').read_bytes(), b'preserved')


if __name__ == '__main__': unittest.main()
