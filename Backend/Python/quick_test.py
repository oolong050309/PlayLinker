"""
快速测试Python环境
检查Python版本和依赖是否正确安装
"""
import sys

print("=" * 60)
print("Python环境快速测试")
print("=" * 60)

# 测试Python版本
print(f"\nPython版本: {sys.version}")
version_info = sys.version_info
if version_info.major >= 3 and version_info.minor >= 8:
    print("✓ Python版本满足要求 (>= 3.8)")
else:
    print("✗ Python版本不满足要求，需要 >= 3.8")
    sys.exit(1)

# 测试依赖
print("\n检查依赖安装:")
dependencies = {
    "xbox.webapi": "xbox-webapi-python",
    "httpx": "httpx",
    "pydantic": "pydantic",
}

all_ok = True
for module, package in dependencies.items():
    try:
        __import__(module.replace(".", "_") if "." in module else module)
        print(f"✓ {package} 已安装")
    except ImportError:
        print(f"✗ {package} 未安装")
        all_ok = False

print("\n" + "=" * 60)
if all_ok:
    print("✓ 所有检查通过，环境配置正确")
    print("\n下一步: 启动后端并调用Xbox认证API")
else:
    print("✗ 部分检查失败")
    print("\n解决方案:")
    print("1. 安装依赖: pip install -r requirements.txt")
    print("2. 或使用国内镜像: pip install -r requirements.txt -i https://pypi.tuna.tsinghua.edu.cn/simple")
print("=" * 60)

sys.exit(0 if all_ok else 1)

