mergeInto(LibraryManager.library, {
  JoinDogChoosePet: function(receiverPtr) {
    var receiver=UTF8ToString(receiverPtr);
    var input=document.createElement('input');
    input.type='file'; input.accept='image/*';
    input.style.display='none'; document.body.appendChild(input);
    input.onchange=function() {
      var file=input.files && input.files[0]; input.remove();
      if(!file) return;
      if(file.size>20*1024*1024) { SendMessage(receiver,'PhotoError','Elige una foto de menos de 20 MB.'); return; }
      var url=URL.createObjectURL(file), img=new Image();
      img.onload=function() {
        var canvas=document.createElement('canvas'); canvas.width=512; canvas.height=512;
        var ctx=canvas.getContext('2d'); ctx.fillStyle='#eee6fc'; ctx.fillRect(0,0,512,512);
        var side=Math.min(img.naturalWidth,img.naturalHeight);
        ctx.drawImage(img,(img.naturalWidth-side)/2,(img.naturalHeight-side)/2,side,side,0,0,512,512);
        var data=canvas.toDataURL('image/jpeg',0.78).split(',')[1];
        URL.revokeObjectURL(url); SendMessage(receiver,'ReceivePhoto',data);
      };
      img.onerror=function() { URL.revokeObjectURL(url); SendMessage(receiver,'PhotoError','Prueba con una foto JPG o PNG.'); };
      img.src=url;
    };
    input.addEventListener('cancel',function(){input.remove();});
    input.click();
  }
});
