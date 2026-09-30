"""One local photo job. No credentials or provider response bodies in logs."""
import argparse
import json
import re
import sys
import traceback
import urllib.error
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from vision.photo_enhance import enhance,local_fallback

def main():
    parser=argparse.ArgumentParser()
    for name in ('input','plate','mask','status'):parser.add_argument('--'+name,required=True,type=Path)
    parser.add_argument('--fallback-local',action='store_true',help='use bounded local fusion if the gateway is unavailable')
    args=parser.parse_args()
    try:
        output,recipe=enhance(args.input,args.plate,args.mask)
        result={'ok':True,'output':str(output),'status':'AI 光色版已另存到 Downloads','model':'gpt-5.6-terra','recipe':recipe}
    except urllib.error.HTTPError as error:
        if args.fallback_local:
            try:
                output,recipe=local_fallback(args.input,args.plate,args.mask)
                result={'ok':True,'fallback':True,'output':str(output),'status':'原图已保存 · AI 网关暂不可用，已完成本地环境光融合','model':'local-fallback','recipe':recipe}
            except Exception:
                result={'ok':False,'status':f'原图已保存 · AI 网关暂不可用（{error.code}）','error_type':'HTTPError'}
        else:
            result={'ok':False,'status':f'原图已保存 · AI 网关暂不可用（{error.code}）','error_type':'HTTPError'}
    except ValueError as error:
        if args.fallback_local:
            try:
                output,recipe=local_fallback(args.input,args.plate,args.mask)
                result={'ok':True,'fallback':True,'output':str(output),'status':'原图已保存 · AI 参数不可用，已完成本地环境光融合','model':'local-fallback','recipe':recipe}
            except Exception:
                status='原图已保存 · 请配置有效 AI 密钥' if str(error) in ('credential_missing','configuration_missing') else '原图已保存 · AI 未返回有效融合参数'
                result={'ok':False,'status':status,'error_type':'ValueError'}
        else:
            status='原图已保存 · 请配置有效 AI 密钥' if str(error) in ('credential_missing','configuration_missing') else '原图已保存 · AI 未返回有效融合参数'
            result={'ok':False,'status':status,'error_type':'ValueError'}
    except Exception as error:
        # Class names identify transport/runtime failures without exposing a
        # request URL, response body, filesystem path or credential.
        cause=error.reason if isinstance(error,urllib.error.URLError) else error
        frames=traceback.extract_tb(getattr(cause,'__traceback__',None))
        operation=frames[-1].name if frames else ''
        tunnel=re.search(r'Tunnel connection failed: (\d{3})',str(cause))
        if args.fallback_local:
            try:
                output,recipe=local_fallback(args.input,args.plate,args.mask)
                result={'ok':True,'fallback':True,'output':str(output),'status':'原图已保存 · AI 暂不可用，已完成本地环境光融合','model':'local-fallback','recipe':recipe}
            except Exception:
                result={'ok':False,'status':'原图已保存 · AI 暂不可用，可继续下一局','error_type':type(cause).__name__,
                        'error_operation':operation if re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]{0,79}',operation) else '',
                        'transport_status':int(tunnel[1]) if tunnel else 0}
        else:
            result={'ok':False,'status':'原图已保存 · AI 暂不可用，可继续下一局','error_type':type(cause).__name__,
                    'error_operation':operation if re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]{0,79}',operation) else '',
                    'transport_status':int(tunnel[1]) if tunnel else 0}
    args.status.write_text(json.dumps(result,ensure_ascii=False))
    return 0 if result['ok'] else 1
if __name__=='__main__':raise SystemExit(main())
