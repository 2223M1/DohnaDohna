import unittest
import tempfile
import json
from pathlib import Path

from organize_voice_table import parse_voice_table, plan_associations, organize, write_csv, TABLE
from extract_reference import sha256, save_bytes, write_report


SAMPLE = '''tree ボイス情報 = {
\t角色甲 = {
\t\t伤害 = {
\t\t\tvoice = (list) { "100", "角色甲_测试、语音" },
\t\t},
\t\t结算 = {
\t\t\tvoice = (list) { 101, "missing" },
\t\t},
\t},
};
'''


def audio(name):
    return {'kind': 'voice', 'original_name': name + '.ogg', 'output': 'assets/audio/voice/' + name + '.ogg',
            'sha256': 'test'}


class VoiceTableTests(unittest.TestCase):
    def test_parse_numeric_named_and_source_lines(self):
        rows = parse_voice_table(SAMPLE)
        self.assertEqual([r['reference'] for r in rows], ['100', '角色甲_测试、语音', '101', 'missing'])
        self.assertEqual(rows[0]['role'], '角色甲')
        self.assertEqual(rows[2]['purpose'], '结算')
        self.assertEqual(rows[0]['source_line'], 4)

    def test_missing_refs_and_unmapped_audio_are_not_guessed(self):
        plans, associations = plan_associations(parse_voice_table(SAMPLE), [audio('100'), audio('101'), audio('999')])
        self.assertEqual([r['status'] for r in associations], ['matched', 'not_in_voice_archive', 'matched', 'not_in_voice_archive'])
        self.assertIn('按角色', plans[0]['output'])
        self.assertIn('未关联', plans[2]['output'])
        self.assertEqual(plans[2]['associations'], [])

    def test_reused_recording_keeps_every_context_and_one_file(self):
        refs = parse_voice_table(SAMPLE)
        refs.append(dict(refs[0], purpose='另一用途'))
        plans, associations = plan_associations(refs, [audio('100')])
        self.assertEqual(len(plans), 1)
        self.assertEqual(len(plans[0]['associations']), 2)
        self.assertEqual(associations[0]['output'], associations[-1]['output'])

    def test_ambiguous_audio_name_is_rejected(self):
        with self.assertRaises(ValueError):
            plan_associations(parse_voice_table(SAMPLE), [audio('100'), audio('100')])

    def test_partial_or_unexpected_table_is_rejected(self):
        for text in (SAMPLE[:-3], SAMPLE.replace('voice = (list)', 'other = (list)'), SAMPLE.replace('"100"', '1.5')):
            with self.assertRaises(ValueError):
                parse_voice_table(text)

    def test_file_migration_updates_manifest_and_is_idempotent(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            save_bytes(root / TABLE, SAMPLE.encode('utf-8'))
            rows = [audio('100'), audio('101'), audio('999')]
            for row in rows:
                output = root / row['output']
                save_bytes(output, row['original_name'].encode('utf-8'))
                row['sha256'] = sha256(output)
            write_report(root / 'reverse/reports/audio_assets.json', rows)
            write_csv(root / 'assets/manifest.csv', rows, ['kind', 'original_name', 'output', 'sha256'])
            first = organize(root)
            self.assertEqual(first['physical_voice_files'], 3)
            self.assertEqual(first['voice_file_counts'], {'table_matched': 2, 'not_in_this_table': 1})
            self.assertEqual(first['sha256_verified'], 3)
            self.assertEqual(len(first['missing_references']), 2)
            second = organize(root)
            self.assertEqual(second['moved_files'], 0)
            self.assertEqual(second['sha256_verified'], 3)
            updated = json.loads((root / 'reverse/reports/audio_assets.json').read_text(encoding='utf-8'))
            for row in updated:
                self.assertEqual(sha256(root / row['output']), row['sha256'])
            self.assertTrue((root / 'reverse/reports/voice_table_before/manifest.csv').is_file())


if __name__ == '__main__':
    unittest.main()
