/* GPL-3.0-or-later. Standalone PS-X EXE audio renderer using Highly Experimental.
 * This program receives an externally supplied BIOS; none is embedded. */
#include <stdint.h>
#include "highly_experimental/psx.h"
#include "highly_experimental/bios.h"
#include "highly_experimental/iop.h"
#include "highly_experimental/r3000.h"
#include "highly_experimental/spu.h"
#ifdef _WIN32
#include <io.h>
#include <fcntl.h>
#endif
static uint32_t get32(const unsigned char *p) {
    return (uint32_t)p[0] | ((uint32_t)p[1]<<8) | ((uint32_t)p[2]<<16) | ((uint32_t)p[3]<<24);
}
static int read32(uint32_t *v) {
    unsigned char b[4];
    if (fread(b,1,4,stdin)!=4) return 0;
    *v=get32(b); return 1;
}
int main(void) {
    uint32_t magic,bios_size,count,refresh,n,i;
    unsigned char *bios,*exe;
    void *state,*iop,*cpu;
    int16_t samples[4096*2];
#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif
    if (!read32(&magic) || magic!=0x31534650 || !read32(&bios_size) || bios_size!=524288 || !read32(&count) || count==0 || count>128 || !read32(&refresh) || (refresh!=50 && refresh!=60)) return 2;
    bios=malloc(bios_size);
    if (!bios || fread(bios,1,bios_size,stdin)!=bios_size) return 2;
    bios_set_image(bios,bios_size);
    if (psx_init()!=0) return 3;
    state=malloc(psx_get_state_size(1));
    if (!state) return 3;
    psx_clear_state(state,1);
    iop=psx_get_iop_state(state);
    cpu=iop_get_r3000_state(iop);
    for(i=0;i<count;i++) {
        uint32_t address;
        if (!read32(&n) || n<2048 || n>2048+0x1f0000) return 2;
        exe=malloc(n);
        if (!exe || fread(exe,1,n,stdin)!=n || memcmp(exe,"PS-X EXE",8)) return 2;
        address=get32(exe+0x18)&0x1fffff;
        if(address<0x10000 || address+(n-2048)>0x200000) return 2;
        iop_upload_to_ram(iop,address,exe+2048,n-2048);
        if(i==0) {
            r3000_setreg(cpu,R3000_REG_PC,get32(exe+0x10));
            r3000_setreg(cpu,R3000_REG_GEN+29,get32(exe+0x30));
        }
        free(exe);
    }
    psx_set_refresh(state,refresh);
    iop_set_compat(iop,IOP_COMPAT_FRIENDLY);
    spu_enable_reverb(iop_get_spu_state(iop),1);
    if(fwrite("PSF1",1,4,stdout)!=4 || fflush(stdout)) return 4;
    while(read32(&n) && n!=0) {
        uint32_t done=0,idle=0;
        if(n>4096) return 2;
        while(done<n) {
            uint32_t frames=n-done;
            int result=psx_execute(state,100000,samples+done*2,&frames,0);
            if(result<0 || (!frames && ++idle>2000)) return 3;
            done+=frames;
        }
        /* The wire format is always little endian, independently of host. */
        for(i=0;i<n*2;i++) {
            uint16_t s=(uint16_t)samples[i];
            if(putchar(s&255)==EOF || putchar(s>>8)==EOF) return 4;
        }
        if(fflush(stdout)) return 4;
    }
    free(state); free(bios); return 0;
}
