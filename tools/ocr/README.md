# ddddocr 验证码识别

协议服务的私有识别进程。Python 3.11/3.12 推荐，CPU 推理，不需要浏览器或 GPU。
`ddddocr` 实例只初始化一次，依赖内置模型；不保存验证码或识别结果。

```sh
python3 -m venv .runtime/ocr-venv
.runtime/ocr-venv/bin/pip install -r tools/ocr/requirements.txt
.runtime/ocr-venv/bin/python tools/ocr/server.py --port 8866
```

Windows 使用 `.runtime/ocr-venv/Scripts/python.exe` 和 `pip.exe`。
服务固定监听 `127.0.0.1`；与协议服务运行在同一主机，不对公网开放。
`GET /health` 只有模型加载完成后才返回 `{"status":"ready"}`。
`POST /recognize` 接收 `{"image":"<BASE64_IMAGE>"}`，返回 `{"code":"<CODE>"}`。
错误图片为 400，推理失败为 422，识别结果是否正确由教务登录响应判断。
代码不转换大小写，不重复登录；每次登录使用同一 Cookie 会话取得的验证码。

设置 `XKE_DDDDOCR_URL=http://127.0.0.1:8866/recognize` 供协议服务调用。
桌面版当前人工验证码不依赖此进程。云部署示例见
[协议服务手册](../../notes/development-docs/personal-runtime/06_protocol_host.md)。

接口测试无需模型：`python -m unittest discover -s tools/ocr -p test_server.py`。
这些测试验证请求契约和错误边界，不衡量学校验证码识别准确率。
