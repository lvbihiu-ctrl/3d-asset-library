package main

import (
 "crypto/sha256"
 "embed"
 "fmt"
 "io"
 "os"
 "os/exec"
 "path/filepath"
 "strings"
 "syscall"
 "unsafe"
)

//go:embed app.ps1
var resources embed.FS

const installDir = "D:\\Codex\\3D-Asset-Library-EXE"
const exeName = "3D素材管理器.exe"

func msg(message string) {
 user32:=syscall.NewLazyDLL("user32.dll")
 proc:=user32.NewProc("MessageBoxW")
 title,_:=syscall.UTF16PtrFromString("3D 素材管理器")
 body,_:=syscall.UTF16PtrFromString(message)
 proc.Call(0,uintptr(unsafe.Pointer(body)),uintptr(unsafe.Pointer(title)),0x10)
}
func copyBinary(src,dst string) error {
 f,e:=os.Open(src);if e!=nil{return e};defer f.Close()
 out,e:=os.OpenFile(dst+".new",os.O_CREATE|os.O_TRUNC|os.O_WRONLY,0644);if e!=nil{return e}
 _,e=io.Copy(out,f);if e!=nil{out.Close();os.Remove(dst+".new");return e}
 e=out.Close();if e!=nil{return e}
 return os.Rename(dst+".new",dst)
}
func sameFile(a,b string) bool {
 return strings.EqualFold(filepath.Clean(a),filepath.Clean(b))
}
func runInstalled(path string) error {
 cmd:=exec.Command(path)
 cmd.Dir=installDir
 return cmd.Start()
}
func main(){
 if _,err:=os.Stat("D:\\");err!=nil{msg("需要 D: 盘。请先确认 D:\\Codex 可用。\n\n本程序不会自动改装到 C 盘。");return}
 if err:=os.MkdirAll(installDir,0755);err!=nil{msg("无法创建安装目录：\n"+err.Error());return}
 self,err:=os.Executable();if err!=nil{msg(err.Error());return}
 target:=filepath.Join(installDir,exeName)
 if !sameFile(self,target){
  data,err:=resources.ReadFile("app.ps1");if err!=nil{msg(err.Error());return}
  signature:=sha256.Sum256(data)
  marker:=filepath.Join(installDir,".runtime","script.sha256")
  present,_:=os.ReadFile(marker)
  if _,err:=os.Stat(target);os.IsNotExist(err) || strings.TrimSpace(string(present))!=fmt.Sprintf("%x",signature){
   if err:=copyBinary(self,target);err!=nil{msg("无法安装客户端：\n"+err.Error());return}
  }
  if err:=runInstalled(target);err!=nil{msg("无法打开已安装客户端：\n"+err.Error())}
  return
 }
 runtimeDir:=filepath.Join(installDir,".runtime")
 if err:=os.MkdirAll(runtimeDir,0755);err!=nil{msg(err.Error());return}
 script,err:=resources.ReadFile("app.ps1");if err!=nil{msg(err.Error());return}
 scriptPath:=filepath.Join(runtimeDir,"app.ps1")
 temp:=scriptPath+".new"
 if err:=os.WriteFile(temp,append([]byte{0xEF,0xBB,0xBF},script...),0644);err!=nil{msg("无法写入运行文件：\n"+err.Error());return}
 if err:=os.Rename(temp,scriptPath);err!=nil{msg(err.Error());return}
 sum:=sha256.Sum256(script);os.WriteFile(filepath.Join(runtimeDir,"script.sha256"),[]byte(fmt.Sprintf("%x",sum)),0644)
 overridePath:=filepath.Join(runtimeDir,"override.ps1")
 overrideHashFile:=filepath.Join(runtimeDir,"override.sha256")
 if want,err:=os.ReadFile(overrideHashFile);err==nil {
  if candidate,err:=os.ReadFile(overridePath);err==nil {
   actual:=sha256.Sum256(candidate)
   if strings.EqualFold(strings.TrimSpace(string(want)),fmt.Sprintf("%x",actual)) {
    scriptPath=overridePath
   }
  }
 }
 powershell:=filepath.Join(os.Getenv("SystemRoot"),"System32\\WindowsPowerShell\\v1.0\\powershell.exe")
 if _,err:=os.Stat(powershell);err!=nil{powershell="powershell.exe"}
 cmd:=exec.Command(powershell,"-NoLogo","-NoProfile","-STA","-ExecutionPolicy","Bypass","-File",scriptPath)
 cmd.Dir=installDir
 logs:=filepath.Join(installDir,"logs")
 os.MkdirAll(logs,0755)
 logFile,err:=os.OpenFile(filepath.Join(logs,"startup.log"),os.O_CREATE|os.O_APPEND|os.O_WRONLY,0644)
 if err==nil {defer logFile.Close();cmd.Stdout=logFile;cmd.Stderr=logFile}
 cmd.SysProcAttr=&syscall.SysProcAttr{HideWindow:true,CreationFlags:0x08000000}
 if err:=cmd.Run();err!=nil{msg("客户端窗口意外退出：\n"+err.Error()+"\n\n请检查 D:\\Codex\\3D-Asset-Library-EXE 下的运行文件。")}
}
