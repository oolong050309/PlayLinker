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
        """处理 GET 请求 - 支持所有路径，因为OAuth回调可能访问任何路径"""
        try:
            # 解析URL路径和查询参数
            parsed_url = urlparse(self.path)
            query_params = parse_qs(parsed_url.query)
            
            # 记录收到的请求（用于调试）
            print(f"DEBUG: 收到回调请求 - Path: {self.path}, Query: {parsed_url.query}", flush=True)
        except Exception as e:
            self.send_error(
                400,
                explain=f"Invalid request='{self.requestline}' - Failed to parse URL Path, error={e}",
            )
            self.end_headers()
            return

        # 检查是否有错误
        if query_params.get("error"):
            error_description = query_params.get("error_description", ["未知错误"])
            error_msg = error_description[0] if isinstance(error_description, list) else str(error_description)
            print(f"ERROR: OAuth回调错误 - {error_msg}", flush=True)
            self.send_error(
                400, 
                explain=f"Auth callback failed - Error: {error_msg}"
            )
            self.end_headers()
            return

        # 获取授权码
        auth_code = query_params.get("code")
        if not auth_code:
            print(f"WARNING: Callback request has no code parameter - Path: {self.path}", flush=True)
            # 如果不是认证回调，返回友好的提示页面
            if "/auth/callback" not in self.path:
                response_html = """<html><head><title>Xbox Auth Callback</title></head><body>
                    <h1>Xbox Authentication Callback Server</h1>
                    <p>This server is used to receive Xbox OAuth authentication callbacks.</p>
                    <p>If you see this page, there may be an issue with the authentication flow.</p>
                    <p>Please check if the authentication URL is correct, or contact technical support.</p>
                    </body></html>"""
                response_body = response_html.encode('utf-8')
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(response_body)))
                self.end_headers()
                self.wfile.write(response_body)
                return
            else:
                self.send_error(
                    400,
                    explain=f"Auth callback failed - No code received - Path: {self.path}",
                )
                self.end_headers()
                return

        # 处理授权码（可能是列表或字符串）
        if isinstance(auth_code, list):
            auth_code = auth_code[0]
        elif isinstance(auth_code, str):
            pass
        else:
            print(f"ERROR: Invalid code parameter type: {type(auth_code)}", flush=True)
            self.send_error(400, explain=f"Invalid code query param: {auth_code}")
            self.end_headers()
            return

        print(f"INFO: Received authorization code, length: {len(auth_code)}", flush=True)
        
        # 将授权码放入队列
        try:
            QUEUE.put(auth_code, timeout=1)
            print("INFO: Authorization code has been put into queue", flush=True)
        except Exception as e:
            print(f"ERROR: Failed to put authorization code into queue: {e}", flush=True)
            self.send_error(500, explain="Internal server error: Failed to process auth code")
            self.end_headers()
            return

        # 返回成功页面（自动关闭窗口）
        response_html = """<html><head><title>Authentication Success</title>
            <script>
                setTimeout(function() {
                    window.close();
                }, 2000);
            </script>
            </head><body>
            <h1>Authentication Successful!</h1>
            <p>You have completed Xbox authentication. This window will close automatically in 2 seconds.</p>
            <p>If the window does not close automatically, you can manually close this tab.</p>
            </body></html>"""
        response_body = response_html.encode('utf-8')
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(response_body)))
        self.end_headers()
        self.wfile.write(response_body)
        print("INFO: 已发送成功响应给浏览器", flush=True)

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
            print(f"DEBUG: 授权码长度: {len(code)}", flush=True)

            # 获取令牌（这一步可能需要一些时间，添加超时和日志）
            print("INFO: 开始调用Xbox API获取访问令牌...", flush=True)
            try:
                await auth_mgr.request_tokens(code)
                print("INFO: 令牌获取成功", flush=True)
            except Exception as e:
                print(f"ERROR: 获取令牌失败: {e}", flush=True)
                raise

        # 保存令牌
        token_dir = os.path.dirname(token_filepath)
        if token_dir and not os.path.exists(token_dir):
            os.makedirs(token_dir, exist_ok=True)

        with open(token_filepath, mode="w", encoding="utf-8") as f:
            # 使用 Pydantic V2 的新方法
            f.write(auth_mgr.oauth.model_dump_json())

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
    # 注意：使用 allow_reuse_address=True 允许端口重用
    socketserver.TCPServer.allow_reuse_address = True
    httpd = socketserver.TCPServer(
        ("0.0.0.0", args.port), AuthCallbackRequestHandler
    )
    
    # 在后台线程中运行服务器
    server_thread = threading.Thread(target=httpd.serve_forever)
    server_thread.daemon = True
    server_thread.start()
    
    # 输出服务器启动信息
    print(json.dumps({
        "info": "http_server_started",
        "port": args.port,
        "message": f"HTTP服务器已启动，监听端口 {args.port}"
    }, ensure_ascii=False), flush=True)

    exit_code = 0

    exit_code = 0
    try:
        await do_auth(
            args.client_id, args.client_secret, args.redirect_uri, args.tokens
        )
        # 认证成功，输出调试信息
        print("DEBUG: 认证流程完成，准备关闭HTTP服务器", flush=True)
    except queue.Empty:
        print(json.dumps({
            "success": False,
            "error": "timeout",
            "message": "认证超时"
        }, ensure_ascii=False), flush=True)
        exit_code = 1
    except KeyboardInterrupt:
        print(json.dumps({
            "success": False,
            "error": "cancelled",
            "message": "认证已取消"
        }, ensure_ascii=False), flush=True)
        exit_code = 0
    except Exception as e:
        print(json.dumps({
            "success": False,
            "error": "exception",
            "message": str(e)
        }, ensure_ascii=False), flush=True)
        exit_code = 1
    finally:
        # 确保服务器关闭
        print("DEBUG: 正在关闭HTTP服务器...", flush=True)
        try:
            httpd.shutdown()
            httpd.server_close()
            print("DEBUG: HTTP服务器已关闭", flush=True)
        except Exception as e:
            print(f"WARNING: 关闭HTTP服务器时出错: {e}", flush=True)
        
        sys.exit(exit_code)


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

