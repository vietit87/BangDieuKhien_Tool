using System;
// Vietnamese astronomical lunar calendar, Ho Ngoc Duc's published algorithm, UTC+7.
internal static class Lunar {
 static int I(double v){return (int)Math.Floor(v);} static double S(double x){return Math.Sin(x*Math.PI/180);}
 public static int Jd(DateTime d){int a=(14-d.Month)/12,y=d.Year+4800-a,m=d.Month+12*a-3;int n=d.Day+(153*m+2)/5+365*y+y/4-y/100+y/400-32045;return n;}
 static int Moon(int k){double t=k/1236.85,t2=t*t,t3=t2*t;double jd=2415020.75933+29.53058868*k+.0001178*t2-.000000155*t3+.00033*S(166.56+132.87*t-.009173*t2);double m=359.2242+29.10535608*k-.0000333*t2-.00000347*t3,p=306.0253+385.81691806*k+.0107306*t2+.00001236*t3,f=21.2964+390.67050646*k-.0016528*t2-.00000239*t3;double c=(.1734-.000393*t)*S(m)+.0021*S(2*m)-.4068*S(p)+.0161*S(2*p)-.0004*S(3*p)+.0104*S(2*f)-.0051*S(m+p)-.0074*S(m-p)+.0004*S(2*f+m)-.0004*S(2*f-m)-.0006*S(2*f+p)+.001*S(2*f-p)+.0005*S(2*p+m);double dt=t < -11 ?.001+.000839*t+.0002261*t2-.00000845*t3-.000000081*t*t3:-.000278+.000265*t+.000262*t2;return I(jd+c-dt+.5+7.0/24);}
 static int Sun(int j){double t=(j-2451545.5-7.0/24)/36525,t2=t*t;double m=357.52910+35999.05030*t-.0001559*t2-.00000048*t*t2,l=280.46645+36000.76983*t+.0003032*t2+(1.914600-.004817*t-.000014*t2)*S(m)+(.019993-.000101*t)*S(2*m)+.000290*S(3*m);l=l*Math.PI/180;l-=Math.PI*2*Math.Floor(l/(Math.PI*2));return I(l/Math.PI*6);}
 static int M11(int y){int k=I((Jd(new DateTime(y,12,31))-2415021)/29.530588853);int n=Moon(k);if(Sun(n)>=9)n=Moon(k-1);return n;}
 static int Leap(int a){int k=I((a-2415021.076998695)/29.530588853+.5),last=0,i=1,arc=Sun(Moon(k+i));do{last=arc;i++;arc=Sun(Moon(k+i));}while(arc!=last&&i<14);return i-1;}
 public static int[] Date(DateTime d){int jd=Jd(d),k=I((jd-2415021.076998695)/29.530588853);while(Moon(k)>jd)k--;while(Moon(k+1)<=jd)k++;int start=Moon(k);int a=M11(d.Year),b=a,y;if(a>=start){y=d.Year;a=M11(d.Year-1);}else{y=d.Year+1;b=M11(d.Year+1);}int day=jd-start+1,diff=I((start-a)/29.0),month=diff+11,leap=0;if(b-a>365){int off=Leap(a);if(diff>=off){month=diff+10;if(diff==off)leap=1;}}if(month>12)month-=12;if(month>=11&&diff<4)y--;return new[]{day,month,y,leap};}
 static string[] Can={"Giáp","Ất","Bính","Đinh","Mậu","Kỷ","Canh","Tân","Nhâm","Quý"}; static string[] Chi={"Tý","Sửu","Dần","Mão","Thìn","Tỵ","Ngọ","Mùi","Thân","Dậu","Tuất","Hợi"};
 public static string Year(int y){return Can[(y+6)%10]+" "+Chi[(y+8)%12];}
 public static string Day(DateTime d){int j=Jd(d);return Can[(j+9)%10]+" "+Chi[(j+1)%12];}
 public static string Hours(DateTime d){string[] patterns={"110100101100","001101001011","110011010010","101100110100","001011001101","010010110011"};string p=patterns[((Jd(d)+1)%12)%6],r="";for(int i=0;i<12;i++)if(p[i]=='1')r+=Chi[i]+" ("+((i*2+23)%24)+"–"+((i*2+1)%24)+"h)   ";return r;}
 public static string Holiday(DateTime d){int[] a=Date(d);string s=d.ToString("dd/MM");if(s=="01/01")return "Tết Dương lịch";if(s=="30/04")return "Ngày Thống nhất";if(s=="01/05")return "Quốc tế Lao động";if(s=="02/09")return "Quốc khánh";if(a[3]==0){if(a[1]==1&&a[0]<=3)return "Tết Nguyên đán";if(a[1]==3&&a[0]==10)return "Giỗ Tổ Hùng Vương";if(a[1]==8&&a[0]==15)return "Tết Trung thu";}return "";}
}

