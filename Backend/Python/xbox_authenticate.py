"""
Xbox Live 认证脚本（服务器部署版本）
令牌保存位置修改为项目内的Tokens目录
"""
import asyncio
import os
import sys
import json
from pathlib import Path

try:
    from xbox.webapi.authentication.manager import AuthenticationManager
    from xbox.webapi.authentication.models import OAuth2TokenResponse
    from xbox.webapi.common.signed_session import SignedSession
    from xbox.webapi.scripts import CLIENT_ID, CLIENT_SECRET, REDIRECT_URI
except ImportError:
    print(json.dumps({
        "success": False,
        "error": "xbox-webapi-python 未安装",
        "message": "请安装: pip install xbox-webapi-python"
    }, ensure_ascii=False))
    sys.exit(1)

import http.server
import queue
import socketserver
import threading
import webbrowser
from urllib.parse import parse_qs, urlparse

QUEUE = queue.Queue(1)


class AuthCallbackRequestHandler(http.server.BaseHTTPRequestHandler):
    """处理认证回调的 HTTP 请求处理器"""

    def do_GET(self):
        """处理 GET 请求"""
        try:
            url_path = self.requestline.split(" ")[1]
            query_params = parse_qs(urlparse(url_path).query)
        except Exception as e:
            self.send_error(
                400,
                explain=f"Invalid request='{self.requestline}' - Failed to parse URL Path, error={e}",
            )
            self.end_headers()
            return

        if query_params.get("error"):
            error_description = query_params.get("error_description")
            self.send_error(
                400, explain=f"Auth callback failed - Error: {error_description}"
            )
            self.end_headers()
            return

        auth_code = query_params.get("code")
        if not auth_code:
            self.send_error(
                400,
                explain=f"Auth callback failed - No code received - Original request: {self.requestline}",
            )
            self.end_headers()
            return

        if isinstance(auth_code, list):
            auth_code = auth_code[0]
        elif isinstance(auth_code, str):
            pass
        else:
            raise Exception(f"Invalid code query param: {auth_code}")

        # 将授权码放入队列
        QUEUE.put(auth_code)
        response_body = b"<script>window.close()</script>"
        self.send_response(200)
        self.send_header("Content-Type", "text/html")
        self.send_header("Content-Length", str(len(response_body)))
        self.end_headers()
        self.wfile.write(response_body)

    def log_message(self, format, *args):
        """禁用日志输出"""
        pass


async def do_auth(
    client_id: str, client_secret: str, redirect_uri: str, token_filepath: str
):
    """执行认证流程"""
    async with SignedSession() as session:
        auth_mgr = AuthenticationManager(session, client_id, client_secret, redirect_uri)

        # 如果令牌文件存在，尝试刷新
        if os.path.exists(token_filepath):
            try:
                with open(token_filepath, "r", encoding="utf-8") as f:
                    tokens = f.read()
                auth_mgr.oauth = OAuth2TokenResponse.model_validate_json(tokens)
                await auth_mgr.refresh_tokens()
                print(json.dumps({
                    "success": True,
                    "message": "令牌刷新成功",
                    "xuid": str(auth_mgr.xsts_token.xuid) if auth_mgr.xsts_token else None,
                    "tokens_path": token_filepath
                }, ensure_ascii=False), flush=True)
                return
            except Exception as e:
                # 刷新失败，继续执行新认证
                pass

        # 如果令牌无效，请求新的
        if not (auth_mgr.xsts_token and auth_mgr.xsts_token.is_valid()):
            auth_url = auth_mgr.generate_authorization_url()
            
            # 输出认证URL供用户手动访问（重要：让用户知道要访问哪个URL）
            print(json.dumps({
                "success": False,
                "need_auth": True,
                "auth_url": auth_url,
                "message": "请在浏览器中打开以下链接进行认证"
            }, ensure_ascii=False), flush=True)
            
            # 再次单独输出URL（便于C#解析和日志记录）
            print(f"AUTH_URL: {auth_url}", flush=True)

            # 尝试打开浏览器（如果可能）
            try:
                webbrowser.open(auth_url)
                print("INFO: 已尝试自动打开浏览器", flush=True)
            except Exception as e:
                print(f"WARNING: 无法自动打开浏览器: {e}", flush=True)
                print("INFO: 请手动复制上面的URL到浏览器", flush=True)

            # 等待授权码
            print("INFO: 等待用户在浏览器中完成认证...", flush=True)
            code = QUEUE.get(timeout=300)  # 5分钟超时
            print("INFO: 已收到授权码，正在获取令牌...", flush=True)

            await auth_mgr.request_tokens(code)

        # 保存令牌
        token_dir = os.path.dirname(token_filepath)
        if token_dir and not os.path.exists(token_dir):
            os.makedirs(token_dir, exist_ok=True)

        with open(token_filepath, mode="w", encoding="utf-8") as f:
            f.write(auth_mgr.oauth.json())

        print(json.dumps({
            "success": True,
            "message": "认证完成",
            "xuid": str(auth_mgr.xsts_token.xuid) if auth_mgr.xsts_token else None,
            "tokens_path": token_filepath
        }, ensure_ascii=False), flush=True)


async def async_main():
    """异步主函数"""
    import argparse

    parser = argparse.ArgumentParser(description="Xbox Live 认证工具")
    parser.add_argument(
        "--tokens",
        "-t",
        required=True,
        help="令牌文件路径"
    )
    parser.add_argument(
        "--client-id",
        "-cid",
        default=os.environ.get("CLIENT_ID", CLIENT_ID),
        help="OAuth2 Client ID"
    )
    parser.add_argument(
        "--client-secret",
        "-cs",
        default=os.environ.get("CLIENT_SECRET", CLIENT_SECRET),
        help="OAuth2 Client Secret"
    )
    parser.add_argument(
        "--redirect-uri",
        "-ru",
        default=os.environ.get("REDIRECT_URI", REDIRECT_URI),
        help="OAuth2 重定向 URI"
    )
    parser.add_argument(
        "--port",
        "-p",
        default=8080,
        type=int,
        help="HTTP 服务器端口"
    )
    args = parser.parse_args()

    # 启动 HTTP 服务器用于接收回调
    with socketserver.TCPServer(
        ("0.0.0.0", args.port), AuthCallbackRequestHandler
    ) as httpd:
        server_thread = threading.Thread(target=httpd.serve_forever)
        server_thread.daemon = True
        server_thread.start()

        try:
            await do_auth(
                args.client_id, args.client_secret, args.redirect_uri, args.tokens
            )
        except queue.Empty:
            print(json.dumps({
                "success": False,
                "error": "timeout",
                "message": "认证超时"
            }, ensure_ascii=False), flush=True)
            sys.exit(1)
        except KeyboardInterrupt:
            print(json.dumps({
                "success": False,
                "error": "cancelled",
                "message": "认证已取消"
            }, ensure_ascii=False), flush=True)
            sys.exit(0)
        except Exception as e:
            print(json.dumps({
                "success": False,
                "error": "exception",
                "message": str(e)
            }, ensure_ascii=False), flush=True)
            sys.exit(1)


def main():
    """主函数"""
    try:
        asyncio.run(async_main())
    except KeyboardInterrupt:
        print(json.dumps({
            "success": False,
            "error": "cancelled",
            "message": "认证已取消"
        }, ensure_ascii=False), flush=True)
        sys.exit(0)


if __name__ == "__main__":
    main()

