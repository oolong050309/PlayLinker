"""
测试Python环境和依赖是否正确安装
运行此脚本检查环境配置
"""
import sys
import os

def test_python_version():
    """测试Python版本"""
    print("=" * 60)
    print("测试Python版本")
    print("=" * 60)
    version = sys.version_info
    print(f"Python版本: {version.major}.{version.minor}.{version.micro}")
    
    if version.major == 3 and version.minor >= 8:
        print("✓ Python版本满足要求 (>= 3.8)")
        return True
    else:
        print("✗ Python版本不满足要求，需要 >= 3.8")
        return False

def test_dependencies():
    """测试依赖是否安装"""
    print("\n" + "=" * 60)
    print("测试依赖安装")
    print("=" * 60)
    
    dependencies = [
        "xbox.webapi",
        "httpx",
        "pydantic",
        "ecdsa",
        "appdirs",
        "ms_cv"
    ]
    
    all_ok = True
    for dep in dependencies:
        try:
            __import__(dep.replace("-", "_"))
            print(f"✓ {dep} 已安装")
        except ImportError:
            print(f"✗ {dep} 未安装")
            all_ok = False
    
    return all_ok

def test_script_files():
    """测试脚本文件是否存在"""
    print("\n" + "=" * 60)
    print("测试脚本文件")
    print("=" * 60)
    
    script_dir = os.path.dirname(os.path.abspath(__file__))
    files = [
        "xbox_authenticate.py",
        "xbox_get_data.py",
        "requirements.txt"
    ]
    
    all_ok = True
    for file in files:
        file_path = os.path.join(script_dir, file)
        if os.path.exists(file_path):
            print(f"✓ {file} 存在")
        else:
            print(f"✗ {file} 不存在")
            all_ok = False
    
    return all_ok

def test_tokens_directory():
    """测试令牌目录"""
    print("\n" + "=" * 60)
    print("测试令牌目录")
    print("=" * 60)
    
    script_dir = os.path.dirname(os.path.abspath(__file__))
    backend_dir = os.path.dirname(script_dir)
    tokens_dir = os.path.join(backend_dir, "Tokens")
    
    print(f"令牌目录路径: {tokens_dir}")
    
    if os.path.exists(tokens_dir):
        print(f"✓ 令牌目录存在")
        if os.access(tokens_dir, os.W_OK):
            print(f"✓ 令牌目录可写")
            return True
        else:
            print(f"✗ 令牌目录不可写")
            return False
    else:
        print(f"⚠ 令牌目录不存在，将在首次认证时自动创建")
        return True

def main():
    """主函数"""
    print("\n" + "=" * 60)
    print("Xbox API Python环境测试")
    print("=" * 60 + "\n")
    
    results = {
        "Python版本": test_python_version(),
        "依赖安装": test_dependencies(),
        "脚本文件": test_script_files(),
        "令牌目录": test_tokens_directory()
    }
    
    print("\n" + "=" * 60)
    print("测试结果汇总")
    print("=" * 60)
    
    all_passed = True
    for test_name, result in results.items():
        status = "通过" if result else "失败"
        symbol = "✓" if result else "✗"
        print(f"{symbol} {test_name}: {status}")
        if not result:
            all_passed = False
    
    print("\n" + "=" * 60)
    if all_passed:
        print("✓ 所有测试通过！环境配置正确。")
        print("\n下一步:")
        print("1. 启动后端服务: cd .. && dotnet run")
        print("2. 打开SwaggerUI: http://localhost:5000/swagger")
        print("3. 调用 POST /api/v1/xbox/authenticate 进行认证")
    else:
        print("✗ 部分测试失败，请根据上述提示解决问题。")
        print("\n常见解决方案:")
        print("1. 安装依赖: pip install -r requirements.txt")
        print("2. 升级Python: 确保使用Python 3.8或更高版本")
        print("3. 检查文件: 确保所有脚本文件存在")
    print("=" * 60)
    
    return 0 if all_passed else 1

if __name__ == "__main__":
    sys.exit(main())

