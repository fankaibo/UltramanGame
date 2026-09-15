"""One local photo job. No credentials or provider response bodies in logs."""
import argparse
import json
import sys
import urllib.error
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from vision.photo_enhance import enhance

def main():
    parser=argparse.ArgumentParser()
    for name in ('input','plate','mask','status'):parser.add_argument('--'+name,required=True,type=Path)
    args=parser.parse_args()
    try:
        output,recipe=enhance(args.input,args.plate,args.mask)
        result={'ok':True,'output':str(output),'status':'AI 融合版已另存到 Downloads','model':'gpt-5.6-terra','recipe':recipe}
    except urllib.error.HTTPError as error:
        result={'ok':False,'status':f'原图已保存 · AI 网关暂不可用（{error.code}）'}
    except ValueError as error:
        status='原图已保存 · 请配置有效 AI 密钥' if str(error) in ('credential_missing','configuration_missing') else '原图已保存 · AI 未返回有效融合参数'
        result={'ok':False,'status':status}
    except Exception:
        result={'ok':False,'status':'原图已保存 · AI 暂不可用，可继续下一局'}
    args.status.write_text(json.dumps(result,ensure_ascii=False))
    return 0 if result['ok'] else 1
if __name__=='__main__':raise SystemExit(main())
