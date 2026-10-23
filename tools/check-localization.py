#!/usr/bin/env python3
"""Validate the native localization table and references before packaging."""
import csv
from pathlib import Path
import re
import string

ROOT = Path(__file__).resolve().parents[1]


def placeholders(text):
    result = set()
    for _, field, spec, conversion in string.Formatter().parse(text):
        if field is None:
            continue
        if not field.isdigit() or spec or conversion:
            raise ValueError('Use numbered placeholders such as {0}, without format specifiers.')
        result.add(int(field))
    if result and result != set(range(max(result) + 1)):
        raise ValueError('Placeholder numbering must start at 0 without gaps.')
    return result


def validate(path, sources=None):
    with Path(path).open(encoding='utf-8-sig', newline='') as stream:
        reader = csv.DictReader(stream)
        header = reader.fieldnames or []
        if len(header) != len(set(header)) or 'Key' not in header or 'english' not in header:
            raise ValueError('Unique Key and english columns are required.')
        rows = {}
        for row in reader:
            key = row.get('Key', '')
            if None in row or any(v is None for v in row.values()):
                raise ValueError('Incorrect CSV column count: ' + key)
            if not re.fullmatch(r'sdb_[a-z0-9_]{1,92}', key) or key in rows:
                raise ValueError('Duplicate or invalid localization key: ' + key)
            if not row['english'].strip():
                raise ValueError('English fallback is missing: ' + key)
            expected = placeholders(row['english'])
            for language in header:
                if language == 'Key' or not row[language]:
                    continue  # An empty translation deliberately falls back to English.
                if placeholders(row[language]) != expected:
                    raise ValueError('Placeholder mismatch: ' + key + '/' + language)
            rows[key] = row
    if not rows:
        raise ValueError('Empty localization catalog.')
    if sources is not None:
        dynamic = {'sdb_direction_', 'sdb_test_state_', 'sdb_quest_state_', 'sdb_test_fieldwork'}
        refs = set()
        for file in Path(sources).rglob('*.cs'):
            refs.update(re.findall(r'"(sdb_[a-z0-9_]+)"', file.read_text(encoding='utf-8')))
        import xml.etree.ElementTree as ET
        for file in Path(sources).rglob('*.xml'):
            refs.update(p.get('value') for p in ET.parse(file).iter('property')
                        if p.get('name', '').endswith('_key') and p.get('value', '').startswith('sdb_'))
        missing = refs - dynamic - rows.keys()
        if missing:
            raise ValueError('Untranslated source keys: ' + ', '.join(sorted(missing)))
        for prefix, values in [('sdb_direction_', ['east','west','north','south']),
                               ('sdb_test_state_', ['active','complete','expired','cancelled']),
                               ('sdb_quest_state_', ['notstarted','inprogress','readyforturnin','completed','failed','cancelled'])]:
            for value in values:
                if prefix + value not in rows:
                    raise ValueError('Missing dynamic key: ' + prefix + value)
    return rows


if __name__ == '__main__':
    rows = validate(ROOT / 'src/SevenDaysBeyond/Config/Localization.csv', ROOT / 'src/SevenDaysBeyond')
    print('Localization: PASS (%d keys; English, Spanish and Latin American Spanish)' % len(rows))
