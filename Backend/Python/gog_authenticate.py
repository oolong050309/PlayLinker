#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GOG认证脚本
通过OAuth2流程进行GOG平台认证,获取并保存访问令牌
"""

import sys
import json
import argparse
import webbrowser
from urllib.parse import urlparse, parse_qs
import requests

# GOG OAuth2认证配置
CLIENT_ID = "46899977096215655"
CLIENT_SECRET = "9d85c43b1482497dbbce61f6e4aa173a433796eeae2ca8c5f6129f2dc4de46d9"
AUTH_URL = "https://auth.gog.com/auth"
TOKEN_URL = "https://auth.gog.com/token"
REDIRECT_URI = "https://embed.gog.com/on_login_success?origin=client"


def print_info(message):
    """打印信息"""
    print(f"INFO: {message}", file=sys.stderr, flush=True)


def print_error(message):
    """打印错误"""
    print(f"ERROR: {message}", file=sys.stderr, flush=True)


def get_authorization_code(open_browser=True):
    """
    获取授权码
    
    Args:
        open_browser: 是否打开浏览器
        
    Returns:
        授权码字符串
    """
    # 构建认证URL
    auth_params = {
        "client_id": CLIENT_ID,
        "redirect_uri": REDIRECT_URI,
        "response_type": "code",
        "layout": "client2"
    }
    
    # 构建完整URL
    params_str = "&".join([f"{k}={v}" for k, v in auth_params.items()])
    auth_url = f"{AUTH_URL}?{params_str}"
    
    print_info("请在浏览器中登录GOG账户...")
    print_info(f"认证URL: {auth_url}")
    
    # 输出认证URL(供C#代码捕获)
    print(f"AUTH_URL:{auth_url}", file=sys.stderr, flush=True)
    
    # 打开浏览器
    if open_browser:
        try:
            webbrowser.open(auth_url)
            print_info("已尝试打开浏览器,如果未自动打开,请手动复制上面的URL到浏览器")
        except Exception as e:
            print_error(f"打开浏览器失败: {e}")
            print_info("请手动复制上面的URL到浏览器")
    else:
        print_info("请手动复制上面的URL到浏览器进行认证")
    
    # 等待用户输入重定向URL
    print_info("登录成功后,请复制浏览器地址栏中的完整URL并粘贴到此处:")
    redirect_url = input().strip()
    
    # 解析URL中的授权码
    try:
        parsed_url = urlparse(redirect_url)
        query_params = parse_qs(parsed_url.query)
        
        if "code" not in query_params:
            raise ValueError("未从重定向URL中找到授权码(code参数)")
        
        auth_code = query_params["code"][0]
        print_info(f"成功获取授权码,长度: {len(auth_code)}")
        return auth_code
    except Exception as e:
        print_error(f"解析授权码失败: {e}")
        raise


def get_token_from_code(auth_code):
    """
    使用授权码获取访问令牌和刷新令牌
    
    Args:
        auth_code: 授权码
        
    Returns:
        包含令牌信息的字典
    """
    print_info("使用授权码获取访问令牌...")
    
    params = {
        "client_id": CLIENT_ID,
        "client_secret": CLIENT_SECRET,
        "grant_type": "authorization_code",
        "code": auth_code,
        "redirect_uri": REDIRECT_URI
    }
    
    try:
        response = requests.get(TOKEN_URL, params=params, timeout=30)
        response.raise_for_status()
        
        token_data = response.json()
        
        # 验证返回数据
        if "access_token" not in token_data:
            raise ValueError("响应中缺少access_token")
        
        if "refresh_token" not in token_data:
            raise ValueError("响应中缺少refresh_token")
        
        print_info("成功获取访问令牌")
        return token_data
        
    except requests.exceptions.RequestException as e:
        print_error(f"获取令牌失败: {e}")
        raise


def refresh_access_token(refresh_token):
    """
    使用刷新令牌获取新的访问令牌
    
    Args:
        refresh_token: 刷新令牌
        
    Returns:
        包含新令牌信息的字典
    """
    print_info("使用刷新令牌获取新访问令牌...")
    
    params = {
        "client_id": CLIENT_ID,
        "client_secret": CLIENT_SECRET,
        "grant_type": "refresh_token",
        "refresh_token": refresh_token
    }
    
    try:
        response = requests.get(TOKEN_URL, params=params, timeout=30)
        response.raise_for_status()
        
        token_data = response.json()
        
        # 验证返回数据
        if "access_token" not in token_data:
            raise ValueError("响应中缺少access_token")
        
        # 刷新令牌可能会更新,需要保存新的刷新令牌
        print_info("成功刷新访问令牌")
        return token_data
        
    except requests.exceptions.RequestException as e:
        print_error(f"刷新令牌失败: {e}")
        raise


def save_tokens(token_data, tokens_path):
    """
    保存令牌到文件
    
    Args:
        token_data: 令牌数据字典
        tokens_path: 令牌文件路径
    """
    print_info(f"保存令牌到文件: {tokens_path}")
    
    try:
        with open(tokens_path, 'w', encoding='utf-8') as f:
            json.dump(token_data, f, ensure_ascii=False, indent=2)
        print_info("令牌已保存")
    except Exception as e:
        print_error(f"保存令牌失败: {e}")
        raise


def load_tokens(tokens_path):
    """
    从文件加载令牌
    
    Args:
        tokens_path: 令牌文件路径
        
    Returns:
        令牌数据字典,如果文件不存在则返回None
    """
    import os
    
    if not os.path.exists(tokens_path):
        print_info("令牌文件不存在")
        return None
    
    try:
        with open(tokens_path, 'r', encoding='utf-8') as f:
            token_data = json.load(f)
        print_info("成功加载现有令牌")
        return token_data
    except Exception as e:
        print_error(f"加载令牌失败: {e}")
        return None


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='GOG认证脚本')
    parser.add_argument('--tokens', required=True, help='令牌文件路径')
    parser.add_argument('--auth-code', help='授权码(从浏览器重定向URL获取)')
    parser.add_argument('--force-reauth', action='store_true', help='强制重新认证')
    
    args = parser.parse_args()
    
    try:
        # 如果提供了授权码,直接使用它换取令牌
        if args.auth_code:
            print_info(f"使用提供的授权码进行认证,授权码长度: {len(args.auth_code)}")
            
            # 直接使用授权码获取令牌
            token_data = get_token_from_code(args.auth_code)
            
            # 保存令牌
            save_tokens(token_data, args.tokens)
            
            # 输出成功结果
            result = {
                "success": True,
                "message": "GOG认证成功",
                "userId": token_data.get("user_id", ""),
                "tokensPath": args.tokens
            }
            print(json.dumps(result, ensure_ascii=False))
            return
        
        # 检查是否已有令牌且不强制重新认证
        if not args.force_reauth:
            existing_tokens = load_tokens(args.tokens)
            if existing_tokens and "refresh_token" in existing_tokens:
                # 尝试刷新令牌
                try:
                    token_data = refresh_access_token(existing_tokens["refresh_token"])
                    
                    # 如果返回了新的刷新令牌,使用新的;否则保留旧的
                    if "refresh_token" not in token_data and "refresh_token" in existing_tokens:
                        token_data["refresh_token"] = existing_tokens["refresh_token"]
                    
                    save_tokens(token_data, args.tokens)
                    
                    # 输出成功结果
                    result = {
                        "success": True,
                        "message": "令牌刷新成功",
                        "userId": token_data.get("user_id", ""),
                        "tokensPath": args.tokens
                    }
                    print(json.dumps(result, ensure_ascii=False))
                    return
                except Exception as e:
                    print_info(f"刷新令牌失败: {e}, 需要提供授权码重新认证")
        
        # 如果没有授权码,返回认证URL
        print_info("未提供授权码,生成认证URL...")
        
        # 构建认证URL
        auth_params = {
            "client_id": CLIENT_ID,
            "redirect_uri": REDIRECT_URI,
            "response_type": "code",
            "layout": "client2"
        }
        params_str = "&".join([f"{k}={v}" for k, v in auth_params.items()])
        auth_url = f"{AUTH_URL}?{params_str}"
        
        # 输出认证URL
        result = {
            "success": False,
            "message": "需要在浏览器中完成认证",
            "authUrl": auth_url,
            "needsAuth": True
        }
        print(json.dumps(result, ensure_ascii=False))
        return
        
    except KeyboardInterrupt:
        print_info("用户取消操作")
        result = {
            "success": False,
            "message": "用户取消认证"
        }
        print(json.dumps(result, ensure_ascii=False))
        sys.exit(1)
        
    except Exception as e:
        print_error(f"认证失败: {e}")
        result = {
            "success": False,
            "message": f"认证失败: {str(e)}"
        }
        print(json.dumps(result, ensure_ascii=False))
        sys.exit(1)


if __name__ == "__main__":
    main()


