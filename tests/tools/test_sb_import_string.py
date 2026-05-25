import unittest

from tools.streamerbot_import import sb_import_string


def encoded_without_padding(name):
    for index in range(100):
        payload = {
            "version": 23,
            "meta": {
                "name": name,
                "description": "x" * index,
            },
            "data": {"actions": []},
        }
        encoded = sb_import_string.encode_sb_payload(payload)
        if not encoded.endswith(b"="):
            return payload, encoded

    raise AssertionError("Could not create an unpadded Streamer.bot import string.")


class StreamerBotImportStringTest(unittest.TestCase):
    def test_decodes_duplicate_identical_import_string_once(self):
        payload, encoded = encoded_without_padding("Duplicated Stub")

        decoded = sb_import_string.decode_sb_bytes(encoded + encoded)

        self.assertEqual(decoded, payload)

    def test_rejects_duplicate_different_import_strings_with_clear_error(self):
        _, first_encoded = encoded_without_padding("First")
        _, second_encoded = encoded_without_padding("Second")
        concatenated = first_encoded + second_encoded

        with self.assertRaisesRegex(ValueError, "multiple different"):
            sb_import_string.decode_sb_bytes(concatenated)


if __name__ == "__main__":
    unittest.main()
