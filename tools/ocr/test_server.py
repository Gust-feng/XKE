import base64
import json
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer

from server import create_handler


class FakeRecognizer:
    def classification(self, image):
        if image != b"test-image":
            raise ValueError("invalid fixture")
        return " aB12 "


class ServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), create_handler(FakeRecognizer()))
        cls.worker = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.worker.start()
        cls.url = f"http://127.0.0.1:{cls.server.server_port}"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.worker.join()

    def request(self, path, data=None):
        request = urllib.request.Request(self.url + path,
                                         data=None if data is None else json.dumps(data).encode(),
                                         headers={"Content-Type": "application/json"})
        try:
            response = urllib.request.urlopen(request, timeout=5)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.status, json.load(response)

    def test_health(self):
        self.assertEqual((200, {"status": "ready"}), self.request("/health"))

    def test_recognition(self):
        self.assertEqual((200, {"code": "aB12"}),
                         self.request("/recognize", {"image": base64.b64encode(b"test-image").decode()}))

    def test_bad_image(self):
        self.assertEqual(400, self.request("/recognize", {"image": "invalid!"})[0])

    def test_recognition_failure(self):
        self.assertEqual(422, self.request("/recognize", {"image": "YQ=="})[0])

    def test_unknown_path(self):
        self.assertEqual(404, self.request("/other", {"image": "YQ=="})[0])


if __name__ == "__main__":
    unittest.main()
