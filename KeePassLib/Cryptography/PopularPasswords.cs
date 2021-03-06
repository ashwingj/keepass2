﻿/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Globalization;
using System.Diagnostics;

using KeePassLib.Utility;

#if !KeePassLibSD
namespace KeePassLib.Cryptography
{
	/// <summary>
	/// Bloom filter-based popular password checking.
	/// </summary>
	public static class PopularPasswords
	{
		private const int PpcTableSize = 8192 * 8; // Bits, multiple of 64

		// Bits set: 32433 of 65536
		// Hash functions: 32
		// Phi (bits unset ratio) estimation: 0.505455388896019
		// Exact Phi: 0.505111694335938
		// False positives ratio: 1.67583063859565E-10
		private static readonly ulong[] PpcTable = {
			0x60383D3A85560B9BUL, 0x2578CE9D37C6AEB7UL, 0xF509A5743FD03228UL,
			0x19B7455E8933EE56UL, 0x5EA419ADCFD9C20EUL, 0xEA618EFC0B37A162UL,
			0xE0FD4D1FFF1CE415UL, 0x7A649E0301BB6060UL, 0x80D9CD9F9EEB603DUL,
			0x47D6010D0D6E6CDEUL, 0x2552708C589EB554UL, 0x073F1A3DB3267502UL,
			0x3313FEC2A2FEA475UL, 0x4593665C44934FEBUL, 0x410A301A23660395UL,
			0x6AD06DA533FF5659UL, 0x423DAF86F3E41F4AUL, 0x82F035A971C6FD18UL,
			0xB5E9139F28C93223UL, 0x1D07C3F4160585CAUL, 0x24B01EDB6B23E2C5UL,
			0xD52F25B724F936C9UL, 0x8018392517836928UL, 0x3AA4C0F8E181EDA2UL,
			0x8D93683EF7D52529UL, 0x6164BB6208114460UL, 0x737A04D8FEF3D88FUL,
			0x3400097098D5C2CBUL, 0x3C2B9ABE5C455B2EUL, 0x3A3819973AB32DA2UL,
			0x38ACB428510AF40BUL, 0x83320D5114B74771UL, 0xC25BEC333B90DCD1UL,
			0x0E9F412FBA3813D1UL, 0x047E31E3098EB2B8UL, 0xBB686AC643F1741FUL,
			0x0BE22E9C0EF0E8F2UL, 0x65AA9504E5F40D31UL, 0xE018DF5D64C62AC7UL,
			0x17020E9A7EFA12EDUL, 0xFC12A7C16006DE82UL, 0x8DE4747E3745346DUL,
			0x31D8C051A43CECAFUL, 0xBE9AFBEF127C1B12UL, 0xAEE94B4B808BBEE2UL,
			0x3A0099CA32835B41UL, 0x59EB3173468D8C49UL, 0x6F89DB1E6DAAE9E1UL,
			0x4C1ADAA837E968E4UL, 0x6E3593A56C682769UL, 0x022AD591689B5B82UL,
			0x4AC33861ED978032UL, 0xF6F476E4E6A1318DUL, 0x2DA690A11AA05A23UL,
			0x916FC56378C29D77UL, 0xAB3238BE22294659UL, 0x2D73A29019B28C77UL,
			0xAAF26C12EC9C3C42UL, 0x058A278A24B334F9UL, 0x033BD18FB8D9BACDUL,
			0x8B3833596008B07CUL, 0x280B6D093333E5E5UL, 0x2128DBE126CA3E1EUL,
			0xCCF09769382472D8UL, 0x0CB6E495BD90CED6UL, 0x1303A37577C01C5AUL,
			0xC8BBF4734FC34C53UL, 0x1B38B72B10F86CD5UL, 0x5098E2D6C1892E51UL,
			0x2DD8065B79DB5380UL, 0x5B9A1A6D6A2292B7UL, 0xC70F751604D0497CUL,
			0x911E08D7363B5213UL, 0x9F2E245273308D2EUL, 0x64D354827957F50EUL,
			0x09856750F560342CUL, 0xDE091F26603F0E70UL, 0xDDE6B4E76173E3B1UL,
			0xC1584AE1B26FA08EUL, 0x1EA29887837838D2UL, 0x6D7643FC67B15C54UL,
			0x921E60571ED103EAUL, 0x63EB1EB33E7AFFF1UL, 0x80BA4D1F95BFD615UL,
			0xEC8A1D4FC1A6B8E0UL, 0x2C46861B6DB17D1AUL, 0x01F05D06927E443BUL,
			0x6172EC2EABEAD454UL, 0x21B8726C6F7C4102UL, 0x3C016CD9945C72ECUL,
			0x708F77B2C0E8B665UL, 0xFC35BE2BB88974DAUL, 0x805897A33702BD61UL,
			0x9A93367A6041226CUL, 0xFDAB188B6158F6BEUL, 0x5F21014A065E918CUL,
			0xF4381DD77772D19CUL, 0xC664B6358AA85011UL, 0xF2639D7B3E2307E6UL,
			0x3FA000D4A5A9C37AUL, 0x8F45D116ED8DC70FUL, 0x8CB8758E45C140D0UL,
			0x49832B46D716236DUL, 0xCC8E4961A93065B8UL, 0x8A996533EDACEB0EUL,
			0x15B35155EC56FAC1UL, 0xE7E0C6C05A9F1885UL, 0x05914F9A1D1C79F9UL,
			0x730000A30B6725F0UL, 0xC95E671F8E543780UL, 0x47D68382400AF94EUL,
			0x1A27F2734FE2249AUL, 0x828079C332D9C0ABUL, 0x2E9BC798EA09170EUL,
			0x6B7CDAC829018C91UL, 0x7B89604901736993UL, 0xABE4EB26F47608F0UL,
			0x70D5FDC88A0FF1B1UL, 0x5A1F0BAB9AB8A158UL, 0xDC89AE0A735C51A4UL,
			0x36C1EA01E9C89B84UL, 0x3A9757AF204096DBUL, 0x1D56C8328540F963UL,
			0x910A8694692472FAUL, 0x697192C9DF145604UL, 0xB20F7A4438712AA2UL,
			0xE8C99185243F4896UL, 0xFBC8970EDBC39CA7UL, 0x33485403868C3761UL,
			0xAFA97DDEDB1D6AD8UL, 0x54A1A6F24476A3BBUL, 0xFE4E078B184BDB7FUL,
			0x5ED1543919754CD8UL, 0x86F8C775160FC08CUL, 0x9B4098F57019040DUL,
			0x039518BBE841327BUL, 0x111D0D420A3F5F6AUL, 0x0666067346AF34ACUL,
			0xD43F1D14EB239B9BUL, 0xA6BB91FEB5618F5BUL, 0xA2B5218B202409ADUL,
			0xC004FA688C3AC25EUL, 0xF0E2D9EA2935E1DCUL, 0x380B31CFA2F2AF43UL,
			0x50E050AE426250EAUL, 0x628ED94D1AA8F55BUL, 0xF8EB0654F0166311UL,
			0x1F8858D26DDA5CC5UL, 0x931425D11CB1EFEBUL, 0xF661D461DC1A05D3UL,
			0x7B75ED7EC6936DA8UL, 0x8713C59690985202UL, 0xF61D6F93F07C0E85UL,
			0xFD1771F6711D6F4FUL, 0x5835A67E1B11419FUL, 0x33EF08ABD56A1050UL,
			0x55B5D0043FA2C01CUL, 0x53316ED963B92D9DUL, 0x6A8C93744E521EDBUL,
			0x083E948062EB9543UL, 0x1C15289B3189AFB1UL, 0xA6A0A5053AE2212DUL,
			0x6573AF7F01FAFF3BUL, 0x58B6F034CFCFE843UL, 0xEB2837CA5AEA6AEDUL,
			0x633E7897097AC328UL, 0x7FA91789B6CCEE82UL, 0xBEE2402F4E7D65EEUL,
			0x616A103EC8FB0DBEUL, 0x65991F9FB25E13FCUL, 0x54EA8A3FADEC1F4BUL,
			0x6D497C5ACDEA0E7AUL, 0x5865045E8CA18527UL, 0xA406C09215ABD61FUL,
			0x68F81F5745FC9875UL, 0xE496D850CEFF3FA9UL, 0xD225C88D63212CB1UL,
			0x37676390525116D2UL, 0x415614AB14188A7DUL, 0xABE58EBC1F6DDC63UL,
			0xDE10312B2C25D28CUL, 0x86C86D7A0B847635UL, 0x408B511D584DC3DCUL,
			0x6711FCC14B303FEDUL, 0x1284DF9CC6972023UL, 0xC3CE0B33141BFA8FUL,
			0x0F3F58367D4A1819UL, 0x9313F83058FBE6D0UL, 0x6FCA5EF39B8E2F47UL,
			0xA90F5C95D887756DUL, 0x96C4E2AD85D5AF6EUL, 0x0ED68A81F526F0A0UL,
			0x53E4472DB4255A35UL, 0xAC581015134D58A6UL, 0x12C000D85C644FC7UL,
			0x124D489B2C0FE6E4UL, 0x8FF83531C6F5D61AUL, 0x132BD6488304F73BUL,
			0x110E99BC59604CB9UL, 0xC28186ACBC940C9BUL, 0x2094C07F48141230UL,
			0x65FB9881A5053589UL, 0x940A3E6D72F09D69UL, 0x972A922CB14BA66EUL,
			0x8D07E59C6DDD4327UL, 0xCB67F993F820157CUL, 0x65B7A54E5FB2ED6CUL,
			0xC235828849576653UL, 0xA695F85479467538UL, 0x9E2BA885E63C4243UL,
			0xDE64A6A5EF84C222UL, 0xC2AB9AF302080621UL, 0x88DBA09B87FA0734UL,
			0xDF002765B44D02E1UL, 0xD50D8D90587CD820UL, 0x1B68B70ED179EFE1UL,
			0xD6E77F8EC26AE95CUL, 0xEE57EB7C45051872UL, 0x4D2B445F36A7F9FDUL,
			0x5502ABB8BB14D7F1UL, 0xAF2C0DF0406FA901UL, 0x6522833444BF4A83UL,
			0xD7CB2E3FC691BE8DUL, 0x4F36F70D2E80D19AUL, 0xF6945FE911D4923BUL,
			0xE3C6FE1EA47399C1UL, 0xF09EA1B2F837702CUL, 0x5122537CF97B5CB5UL,
			0x0C8202B70E9BF154UL, 0x68B554AB58EB5E68UL, 0x7BF9B8052C9BEADEUL,
			0x33612BFCD303810DUL, 0x03E38CF67B37DC53UL, 0x2BFDFF8691F37D5CUL,
			0x4AB483D1CB1D07F6UL, 0xF071A58640639A5CUL, 0x9D6B98169B745CE1UL,
			0x5F42D3E870FDCD93UL, 0x4EDF04404F258238UL, 0x2EAB6E10D65C9BB3UL,
			0x5BB71411EF78DAD2UL, 0x0DE8128636A5D689UL, 0x18FDD1F484DC9365UL,
			0x9896B8896941DA5BUL, 0x8BEF8E3BA4448E5FUL, 0x963A1E977CB1D2CAUL,
			0x02BCF5F068D52851UL, 0x0CD783F09BFBE381UL, 0x350DA833D8C5DB47UL,
			0x8D444C914D795C43UL, 0x8A00B4DFC44D9476UL, 0x4B36CBEC089E55FDUL,
			0xD9D2FA1B0AC66211UL, 0x6C7FC30FA31A8B60UL, 0x9EF4504CC985AD6BUL,
			0x8F2E7E5E0C00EE73UL, 0x819131CFEEBEA069UL, 0xB1E406A863E7A1B4UL,
			0x501F072FF1F2AB67UL, 0xDE578BFC5ADBC264UL, 0xCDD66A09C8E13881UL,
			0x4D443460CE52957FUL, 0x3B198C267976ECFAUL, 0x6B98323D8BD26522UL,
			0x80161F6A489E4BF8UL, 0xE03A8AFCC7AE6872UL, 0x2484BD95A305AB27UL,
			0x6ADDAA46BF25DD0EUL, 0xA429D8B00100477CUL, 0x55AEDB88A074BF2CUL,
			0x63D9F9021AB8F5F3UL, 0x37858538A10C265CUL, 0xEF54C2CE9D063149UL,
			0xFA5CE5AF33E2C136UL, 0xE601A559D0C391D7UL, 0x7C4ED29BBF57DC7EUL,
			0x8FD0D4146DE9E900UL, 0xB58ABFA6CE6C0733UL, 0xF8D7F7743B33EAFFUL,
			0x453FA782F454643CUL, 0xD01752C21AF21E66UL, 0xA50BB7913EAF05DFUL,
			0x966D5B140B2F4189UL, 0x956F5638AFF3D148UL, 0x93FAA838420E8AB3UL,
			0x715E26043071EABDUL, 0x01E7B458B5FD3A41UL, 0x5CFA99C4CC0492AAUL,
			0x761FD391C3623044UL, 0xD39E44E9DB96B5BCUL, 0x8806C544F0534A07UL,
			0x9B225CAFE97EAAC1UL, 0xEAE5E18583492767UL, 0x6B4E51E4C297F096UL,
			0xFC512662EF47E41DUL, 0xB6AC60427DB46F8BUL, 0x8F137F3DB4429C9DUL,
			0x04C65FBEAE9FD8D0UL, 0xEB72305958AE5022UL, 0xAA93AA14BCA2961EUL,
			0x6C7547F9456CA37AUL, 0xEE6094871615BA34UL, 0x489BC8EDE0940402UL,
			0x1108AEFAAD892229UL, 0x673B8B1CF6BED23EUL, 0xFDB781A75FD94DEAUL,
			0x11D9E0F5D914A7BEUL, 0x02830D07F018143DUL, 0x9B3163B8188FD2BAUL,
			0x32C1BEC97D06117EUL, 0x697268B761240CFFUL, 0xBD89CE3037C2E7A9UL,
			0xF21C158125B19600UL, 0x632CB1931601B70AUL, 0x7BB3FB131338085CUL,
			0xA9C06689B8138384UL, 0x161CCBF83EBDC2A1UL, 0x2CF83C01A80B7935UL,
			0x9E51FE393B8E2FF0UL, 0xFE96E52B1606C1A7UL, 0x5E20DFB87F81ACCEUL,
			0xF95DB9602CDAE467UL, 0xDEA155CD35555FEBUL, 0xF0669B810F70CDC6UL,
			0xD36C2FBEB6A449ACUL, 0xCE500C6621C0A445UL, 0x41308909E366460AUL,
			0xAC4D8178DA0CEC24UL, 0xC69049179ED09F7DUL, 0x36B608A0BA2FD848UL,
			0xDF511894DD9568B4UL, 0xB3BFDF78EC861A6CUL, 0xCD50F39D19848153UL,
			0xD2C1BC57E78A408CUL, 0x1E6613EFBB11B5EBUL, 0xF58E30D2D90F73D3UL,
			0xCCB5E2F5E168D742UL, 0xEE97259469BDB672UL, 0x6784D35AF65935A8UL,
			0x71032765ADED1FE8UL, 0x4BBF2FE54D9B72E3UL, 0x5A1BB7831E876A05UL,
			0x12A8FC949EE91686UL, 0x8296F8FA83BD112CUL, 0xAAA7E3BFF64D34D5UL,
			0x0301655E1794EE4BUL, 0x1E547C011BBF30E1UL, 0x39D74FEC536F31D6UL,
			0x3C31A7478B1815BAUL, 0x525C774F82D5836EUL, 0xECF7186DC612FD8CUL,
			0x96B7C4EDD1F3536FUL, 0x7E8C21F19C08541CUL, 0xEE92DB0CF91E4B09UL,
			0xF666190D1591AE5DUL, 0x5E9B45102C895361UL, 0x9A95597AAE5C905DUL,
			0x6E1272E5BB93F93FUL, 0x0E39E612402BFCF8UL, 0x576C9E8CA2A3B35EUL,
			0x7E2E629996D0C35FUL, 0xC95DFF54E3524FCCUL, 0x260F9DEBDEB0E5CBUL,
			0x577B6C6640BAF1ABUL, 0xCA76677779CA358EUL, 0x9E2714BEBCFDB144UL,
			0xD660595CE30FD3EEUL, 0x72DE172D55A5706EUL, 0xB4C84D564489D420UL,
			0x160AA2B9399D5A9DUL, 0x2906ECE619DAC4D2UL, 0x12CE8E8E68A4C317UL,
			0x6BE2DFE89901CAA1UL, 0xEE1D68158102EB77UL, 0x64EB75E45BDA1AC5UL,
			0xEFECF9F98720B55DUL, 0x41CDF813931315BFUL, 0x5F1E4F50CF98FFD4UL,
			0xE69E09EED12E173BUL, 0x89A3707F0396FF65UL, 0x81E36B9DF4FFB492UL,
			0x58C32E883D4DE6DDUL, 0x2D4725C2A5F0B469UL, 0x6B2B9C27CC421CACUL,
			0x3C30F2AD966800C7UL, 0xFF74938BB76B8A7CUL, 0x52B5C99114FD93FAUL,
			0x51647EDCA6C104DAUL, 0xEB47684CF796DF4EUL, 0x376D74A5AB14BD71UL,
			0xF0871FEF8E9DAA3EUL, 0x1D65B134B2E045B6UL, 0x9DC8C0D8623BBA48UL,
			0xAD6FC3C59DBDADF4UL, 0x66F6EBA55488B569UL, 0xB00D53E0E2D38F0AUL,
			0x43A4212CEAD34593UL, 0x44724185FF7019FFUL, 0x50F46061432B3635UL,
			0x880AA4C24E6B320BUL, 0xCAFCB3409A0DB43FUL, 0xA7F1A13DEF47514BUL,
			0x3DC8A385A698220CUL, 0xFA17F82E30B85580UL, 0x430E7F0E88655F47UL,
			0x45A1566013837B47UL, 0x84B2306D2292804EUL, 0xE7A3AF21D074E419UL,
			0x09D08E2C5E569D4DUL, 0x84228F8908383FA2UL, 0xC34079610C8D3E82UL,
			0x66C96426C54A5453UL, 0xD41F164117D32C93UL, 0x7829A66BF1FEC186UL,
			0x4BB6846694BDFC18UL, 0x857D1C1C01352C01UL, 0xAB8E68BA85402A45UL,
			0x74B3C4F101FE76C8UL, 0x6CF482CFAFB29FFEUL, 0x28B174A18F4DC3D1UL,
			0x833C3425B2AA3755UL, 0x8AA58A32747F4432UL, 0xFE7B9FB4BCE3CD58UL,
			0xB0836B2C16FA5553UL, 0x1D08EE6861BF3F23UL, 0x0FAE41E914562DF3UL,
			0xB10A2E041937FC57UL, 0xDA60BB363415BF4CUL, 0xEEC67DBAB4CF4F0AUL,
			0x9A6ED59FCC923B5CUL, 0x9A913C01A8EC7A83UL, 0xAD4779F2F9C7721FUL,
			0x2BF0B7D105BE7459UL, 0x189EFA9AD5195EC6UL, 0xB5C9A2DD64B2A903UL,
			0x5BCD642B2C2FD32CUL, 0xFED3FBF78CB0891FUL, 0x1ED958EE3C36DD3FUL,
			0x030F5DE9CA65E97CUL, 0xBB5BCF8C931B85FEUL, 0xFD128759EA1D8061UL,
			0x2C0238AC416BE6BCUL, 0xBB018584EEACFA27UL, 0xCEA7288C1964DE15UL,
			0x7EA5C3840F29AA4DUL, 0x5DA841BA609E4A50UL, 0xE53AF84845985EB1UL,
			0x93264DA9487183E4UL, 0xC3A4E367AF6D8D15UL, 0xDD4EB6450577BAF8UL,
			0x2AA3093EE2C658ACUL, 0x3D036EC45055C580UL, 0xDDEDB34341C5B7DFUL,
			0x524FFBDC4A1FAC90UL, 0x1B9D63DE13D82907UL, 0x69F9BAF0E868B640UL,
			0xFC1A453A9253013CUL, 0x08B900DECAA77377UL, 0xFF24C72324153C59UL,
			0x6182C1285C507A9BUL, 0x4E6680A54A03CCC8UL, 0x7165680200B67F1FUL,
			0xC3290B26A07DCE5BUL, 0x2AD16584AA5BECE9UL, 0x5F10DF677C91B05EUL,
			0x4BE1B0E2334B198AUL, 0xEA2466E4F4E4406DUL, 0x6ECAA92FF91E6F1DUL,
			0x0267738EFA75CADDUL, 0x4282ED10A0EBFCF2UL, 0xD3F84CE8E1685271UL,
			0xB667ED35716CA215UL, 0x97B4623D70DB7FA8UL, 0xB7BA3AA5E6C2E7CBUL,
			0x8942B2F97118255BUL, 0x009050F842FB52ADUL, 0x114F5511999F5BD5UL,
			0x70C1CAAF1E83F00AUL, 0xAC8EE25D462BB1AAUL, 0x63EEF42AD4E1BED9UL,
			0x58DFBB3D22D3D1A5UL, 0x82B0027C0C63D816UL, 0x48D038F08F3D848BUL,
			0xCE262D5F9A12610EUL, 0xA54BF51C21BD0167UL, 0xF3645F6FB948397DUL,
			0x9188AE58532DA501UL, 0xEC90B0E1479DB767UL, 0x37F4886B83724F80UL,
			0x232B8FF20ACD95AFUL, 0x88A228285D6BCDF0UL, 0x321FB91600259AEEUL,
			0xA1F875F161D18E5EUL, 0x5B6087CDA21AEA0CUL, 0x0156923ED1A3D5F1UL,
			0xC2892C8A6133B5D3UL, 0x015CA4DF0EA6354DUL, 0x5E25EB261B69A7D4UL,
			0xAAA8CF0C012EFBA7UL, 0xCF3466248C37868BUL, 0x0D744514BD1D82C0UL,
			0xB00FF1431EDDF490UL, 0xC79B86A0E3A8AB08UL, 0xFC361529BC9F1252UL,
			0x869285653FB82865UL, 0x9F1C7A17546339ABUL, 0xE31AA66DBD5C4760UL,
			0x51B9D2A765E0FC31UL, 0x31F39528C4CD13D8UL, 0x16C6C35B0D3A341DUL,
			0x90296B1B0F28E2CDUL, 0x36338472A8DB5830UL, 0xA648E6D44DF14F87UL,
			0x93E231E65EB1823FUL, 0x95AA7B9D08E2B627UL, 0x7932D149374700C7UL,
			0x09EFE0A8BF245193UL, 0x742AA63BCEAFD6D8UL, 0x82D4BC5FEDF158B7UL,
			0x02CDEA673CFF150DUL, 0xD8D7B5813B602D15UL, 0xA5A7B670EF15A5EDUL,
			0x4C08E580A1D46AF2UL, 0xC3CA9B905D035647UL, 0x6A39ABB02F6F1B83UL,
			0xD2EC2169F4D02436UL, 0x8E6AEA4DF8515AF2UL, 0x7B3DD4A8693CA2DAUL,
			0xC2ABF17A50AEC383UL, 0xD4FB84F8B6D4F709UL, 0x2839A3EAA2E4C8A7UL,
			0x5D5FD278FE10E1E9UL, 0x5610DDF74125D5A7UL, 0xA484B0B83461DCEAUL,
			0xA511920C0A502369UL, 0xC53F30C6A5394CA4UL, 0x528799285D304DD4UL,
			0xF6D7914CB252BB48UL, 0x892129CB52E65D15UL, 0x15A81B70519ACE6FUL,
			0x5CFBFFD7A2A1C630UL, 0x3B900509C82DF46DUL, 0x19C3CE05D66D5FFCUL,
			0x937D521A4A4799A0UL, 0xD0AE34A6EAD7207DUL, 0x3258A69F1D1A1B38UL,
			0xB173E3255723CC02UL, 0xD7E48FEF7F414F1BUL, 0xDCEBA75F5C761ABEUL,
			0x6DA10C618DEA0D17UL, 0x423FA8B05954FBD1UL, 0x7E73C2E7D923F3C9UL,
			0xC22E21C927C684D1UL, 0x756BAA758764685FUL, 0x8C90B4C4E741D880UL,
			0x1B658C9F4B41D846UL, 0x5D80C14094366707UL, 0xB55FED3E03C00F2DUL,
			0x9B69EB7964C69C83UL, 0x356ED81C9494DADDUL, 0x7599AFF0B2A339D6UL,
			0xA5EBFD25C9B5816BUL, 0xA481DC1C8995E1EFUL, 0xE42C63DF0D402397UL,
			0x3B497B4C30873BAAUL, 0xA950B78BA8772C96UL, 0xD46308D4C76F115DUL,
			0x73714A4ACA76A857UL, 0x0DA86B958FF8CB7DUL, 0xEB61F617B90E0A75UL,
			0xD6106C9B39F51F55UL, 0xB179F73A6BD23B59UL, 0xE7F056E50104A460UL,
			0xBC5B5387634A8642UL, 0xE1678D8752996AF4UL, 0xB508F3D394664A4BUL,
			0xC88536DC4A219B0FUL, 0x39964CBB8CE367B1UL, 0xD51E211D5E9E1417UL,
			0x6821B97B496870F2UL, 0xA596257350CA0A99UL, 0x6D051EE2C49D4D1DUL,
			0xCB426AD61AA8D9B5UL, 0x5FFD3A4062B06D22UL, 0xDAD37BF2A4C594EBUL,
			0x6B9CC848E2B0C686UL, 0x19B4232F3BC622AEUL, 0x70C13C7E5950B702UL,
			0x383318CA622101ACUL, 0xD9647C028CD1C4DFUL, 0x006D123BC553B93CUL,
			0x2CA9D7D896EAE722UL, 0xF19872AC8A0BD5A8UL, 0x59838578EB9E8E5CUL,
			0xB948621EE99B27D4UL, 0x2B470E6036E0E387UL, 0xD0A7E8F0C8A32A84UL,
			0xCBF869271A8E0914UL, 0x705F76A5EA4437CFUL, 0xBAD2BF4933215152UL,
			0xE52EDE847038EA23UL, 0xB8A3EFD3D58D7607UL, 0x748472F5AD330239UL,
			0xCC079CFD428690F6UL, 0x3687450CB7534DACUL, 0x0FEF82D5CC8ACE2AUL,
			0x214653D5C552CA9AUL, 0x9FCA4E87BF6A04FDUL, 0x78D4B114D234A0D7UL,
			0x22840422BD6A5BB5UL, 0x5B9ABE0DE1B4410FUL, 0xB3B50007963FDD6BUL,
			0x53A8A46793B68E35UL, 0x8CDD8E8D188B6033UL, 0x5DD22B6E3ED49572UL,
			0xE561F5D27F5302D6UL, 0xDF89CEC3322E56CDUL, 0x87167F503D600F90UL,
			0x1698BB71C8201862UL, 0xF7BF5DFDB023108EUL, 0xA17FB15B66ACFB5FUL,
			0x2DD771987768073BUL, 0x19299D2D86E0EB29UL, 0x8B537B7F206EED29UL,
			0xE536DA153325ABFCUL, 0x30A69976796DB3B9UL, 0x8E65A2C94E2D4981UL,
			0xC301D53553BD6514UL, 0x46DF3639B9E43790UL, 0x3004CD0E5AFD0463UL,
			0x46E460B0F6ACA1A0UL, 0xCBA210E7372D9BD5UL, 0x45064274A74CA582UL,
			0xFDD57EA43CE631AEUL, 0xF2BA08FFA4A683D0UL, 0x8DA658C4DAD42999UL,
			0x7418042943E88040UL, 0x96000F72E9371FEFUL, 0xE9F1212DC8F47302UL,
			0x2AFB565ECC3553EDUL, 0xCD3D55137EFF7FD6UL, 0xD36F11059388E442UL,
			0xC4B47515DB5709DDUL, 0x5C363EFBF0BAAB67UL, 0x28C63B5A31650BBBUL,
			0x6AE54E5068061C81UL, 0xDEE62000F4E0AA21UL, 0xE8238672FE088A8BUL,
			0x9869CB6370F075B9UL, 0xBA376E2FC7DB330FUL, 0xB0F73E208487CDEEUL,
			0x359D5017BE37FE97UL, 0x684D828C7F95E2DCUL, 0x9985ECA20E46AE1FUL,
			0x8030A5137D1A21C4UL, 0xF78CDC00FC37AC39UL, 0x41CDDC8E61D9C644UL,
			0xB6F3CD1D833BAD1DUL, 0x301D0D858A23DE22UL, 0xA51FCA12AD849BC8UL,
			0x9F55E615986AB10EUL, 0x904AAA59854F2215UL, 0x12ECEA4AB40F51A7UL,
			0xB4EDF5807735E23BUL, 0x6190200F1C589478UL, 0xA3CA57F321909A5AUL,
			0x0BFAEE04B7325B63UL, 0x10C393E7FBCF826DUL, 0x4050A2CA53FDA708UL,
			0xF31114A9B462B680UL, 0x6FB9A6F121BA2006UL, 0x04550CF09389D602UL,
			0xB8C7D6D8CA8942F7UL, 0x71BB430C6436E9D1UL, 0xD9070DD5FAA0A10AUL,
			0x8FD6827757D07E5BUL, 0xD04E6C313F8FD974UL, 0x2CFDEA1187909B9AUL,
			0xC7A8E758C115F593UL, 0xA79A17663009ACC2UL, 0x8091A6B5372B141DUL,
			0xEB33B08767F5BA73UL, 0xDAC1F6823B6111C7UL, 0x697DF90C3515611BUL,
			0xCC1005F198761F48UL, 0x5067E4F5303B45A1UL, 0x04816D292A2D9AC2UL,
			0x2949C7A0874DD5E9UL, 0x25DB2547804CEE5EUL, 0x7EDC3A8946D6F229UL,
			0x00B586F67FD0C622UL, 0x3CAE5798E40324E0UL, 0x0A4F1437DE637164UL,
			0x5F59B2B715871981UL, 0x5D68FF31051E48FBUL, 0x0F2C369D73A2AA46UL,
			0xB009C6B53DD23399UL, 0xC366A81277084988UL, 0x5AF0E0CA0711E730UL,
			0x7AD831A9E9E854BAUL, 0x1DD5EDB0CA4E85AEUL, 0x54651209D259E9DDUL,
			0x3EBB1D9DAB237EADUL, 0xDA96989317AC464CUL, 0xBFCB0F8FBC52C74EUL,
			0x9597ACB9E27B692EUL, 0x6F436B1643C95B23UL, 0xB81A1253E1C3CD9DUL,
			0x7B35F37E905EC67EUL, 0x29CE62666EDA76DDUL, 0xFF2490DC1EC4014DUL,
			0x2D4FF9124DD6B5C4UL, 0xB9510FEC23E0E9D1UL, 0x8BCDBC56541ED071UL,
			0x5414E097C1B0CCB2UL, 0x82BEF8213076F5C7UL, 0xE9FC9A71BD512615UL,
			0xCF15ECC39490DF5AUL, 0x49FA9328D8EE97DBUL, 0x5F80FF0153BC2145UL,
			0xF63BA156B55BCB02UL, 0x0E3B9113109FDF36UL, 0x8FCD6528F54EDE69UL,
			0x5D6AE9C000054763UL, 0x326D873633431FBBUL, 0x380E07FFCEF7A978UL,
			0xDCAA09874A1DF230UL, 0x601494F49F6D261EUL, 0x856159486C9B60E3UL,
			0x85C7F822D07089A5UL, 0xAFFB99CF5AB836C2UL, 0x241AD414FBBB956BUL,
			0x0CFC042822831692UL, 0x382B16D049727FF2UL, 0x784F9997633C819AUL,
			0x5C40ED725F6C390AUL, 0x2CE78B7A3331BA9CUL, 0x9C80636639963B41UL,
			0x1B2D41C968355018UL, 0xD189B57691FB60E4UL, 0x3BD599A9DD85CE31UL,
			0x46FC8E2EF0B9A77CUL, 0x1A389E07D0075EA4UL, 0x1622CA52401DF2ACUL,
			0x528F3FF9B7993BFAUL, 0xF16C176CCA292DDBUL, 0x6C154010961EF542UL,
			0x04B78E92BF6C82DFUL, 0x7D9AFEA6ABB46072UL, 0x3BC573291CBFFC2EUL,
			0x277FFF096D567AF3UL, 0x1CBEB86841A6F757UL, 0xD0BCD49E76CA20A7UL,
			0x25B6024756B1FE90UL, 0xE685C04EF84881FBUL, 0xDCAB14B352FC442EUL,
			0x4FF80A521719953DUL, 0xD10425E411DBE94BUL, 0x60998D0507D6E38DUL,
			0x146AA432C981BD5EUL, 0x1729A596282AAA41UL, 0x152BE1A263BAF963UL,
			0x15278DF497D254CAUL, 0xE4B5E9891E88A5DAUL, 0x087FA3472067D0ACUL,
			0xD99C2899A0AD9158UL, 0x5040F234DC531236UL, 0x9D7E1531259EEE90UL,
			0x29AFB8B49391036EUL, 0x84B619599642D68EUL, 0xE838AAE0F249545CUL,
			0x42D524BA8BB96959UL, 0x9A5B3E817A20EE59UL, 0x584F0530EC4C566BUL,
			0xD6544FD14B47F945UL, 0x3613FB3B553A7CDEUL, 0x284E92B56831AA56UL,
			0xCEE89BA10E951A22UL, 0x476806FA1A8A44E0UL, 0xC84CEF151885C1DFUL,
			0x3DB1D5C1B0B73936UL, 0x45D2D90FDF452388UL, 0x038A7DD71BC5DD21UL,
			0x2AC90C7422C56819UL, 0x4742046638ECE0FBUL, 0x553B44360FC8495DUL,
			0xC8DBA1CF3F9A6E97UL, 0xF85919F494CAB021UL, 0x1479455C2FF236AFUL,
			0x29BCAD159F7D018DUL, 0x016DFF51CBA3BCC5UL, 0x234BF8A77F6B1CF5UL,
			0x20564C6F44F9E641UL, 0x063A550C5AA50FA8UL, 0xB063D0AAAA96DFECUL,
			0x3EC659DF42C092F8UL, 0x29D4A76A5A5F7E09UL, 0x65EFF3EE6E691D1EUL,
			0xBD1634F5721CF799UL, 0xE85BD016723B43FFUL, 0x5233E9E7AEA11022UL,
			0x8C68852EA9039B4CUL, 0x2C978ADBE885BC15UL, 0x726615ED9D497550UL,
			0x7C1E145EB8D2BD96UL, 0xC2FEFB25935A5D71UL, 0x9EE9C3E1C3DE416FUL,
			0xFFD568A03E20E0B3UL, 0xF53649AD90156F2AUL, 0x0331B91DCE54E7EDUL,
			0x67CED5A86E99392FUL, 0x16FC0A5815500B05UL, 0x030392E8D24A7C00UL,
			0x232E5E31DF32A7B5UL, 0xCC8BF22B1947DF21UL, 0x4EC2C72D9C1EEABDUL,
			0x0B1B79F45220E668UL, 0xCC3CF0EE9C4A899BUL, 0xFC260A60592EBC80UL,
			0xC1989A0382CB03EDUL, 0x35FE679A6CD800B2UL, 0x8A6B1ADE4FBB162FUL,
			0xB0FD284563625295UL, 0xCDCC1C7B2181D024UL, 0x5B8BA0C895C0BB23UL,
			0xA681FEA9ADCD43DBUL, 0x0FE30FB6876DE718UL, 0x6DDD1E27B769C494UL,
			0x83A1E58460FFE8BBUL, 0x8FAD6FD2DC90FF65UL, 0x41BB28B81201CB24UL,
			0xA148CE79B2597204UL, 0x7CB87DF97BB477A6UL, 0x9F79E6DED87DC688UL,
			0xE044D84A6C758171UL, 0x1A29E750D9EC4097UL, 0x8445FC2B80C4A0F5UL,
			0x5EFD9784AFED4ED2UL, 0x344C252BD90EB0E4UL, 0xEAD18D2E4418E5B5UL,
			0x207EF4FFC260BD24UL, 0xD2E5C3AE534EC538UL, 0x2F5A59BF3D10E7E1UL,
			0x9528E29266C2924CUL, 0x0121B6BDAB45D138UL, 0xADD0256ACBC771DDUL,
			0x7301769600C6C50DUL, 0x3E7404EA8231D497UL, 0xB39B3840B8D03117UL,
			0x56EFCEDDEF5B6634UL, 0xE6BE2C0D73B72098UL, 0x5A2841A21A5E4959UL,
			0xCFEB3586156DF6E0UL, 0xD84F58901E2D65B8UL, 0x79796786CCC59703UL,
			0x13BFA9A94DD07696UL, 0x7B63116A6B5458B6UL, 0x1406628176C932E0UL,
			0xDD7ACC4E97F91B1AUL, 0xC82B8F84A56BDBE8UL, 0x325D87D08ED8B0B0UL,
			0x3F7847B1E82002DDUL, 0x4662900D2ADAF6BFUL, 0x12AE9F58561DB1D7UL,
			0xA896E956A95CC074UL, 0xAA4FA3A2F8BA4084UL, 0x1D577E35F5DCA67FUL,
			0x796FF2D75469DEC2UL, 0xBD3F3F87E4DE894EUL, 0x3666B2262DEBFB6BUL,
			0x1E26D7AEEF976C2EUL, 0x6BC3854F867AC4A0UL, 0x743DBF8C2E95A821UL,
			0xA62A76B9AE2E645AUL, 0xB4D76561F40187C1UL, 0xD3E5F23F9FA5DB25UL,
			0x34B1F6B39E6A87E2UL, 0x7DA5C3DFF7BE72CFUL, 0xFDF46B1BE80AD4F9UL,
			0x0B21121CA9653B8AUL, 0x1199CA9D0A90F21AUL, 0x6021EA302D01E0BAUL,
			0x8101D063C05CF5D4UL, 0xE2652410DFE78F23UL, 0x84095ACF47C21A25UL,
			0xD7E29A4DB2FD3A99UL, 0x7793C0CB57959F93UL, 0x94C605308B9E5AA7UL,
			0x943DB1AC54165B8FUL, 0xC1391A544C07447CUL, 0x3FEF1A61F785D97BUL,
			0x6DFCC3152478BDE4UL, 0x312AFB0E1982933AUL, 0xB8069C2605631ED3UL,
			0x5A6076423430BED2UL, 0x34E379F09E2D4F42UL, 0x9167F5E4019573E3UL,
			0x18F81157828D2A49UL, 0xF4A8723B4955EAB8UL, 0x0BE6C0ABFEA9E8A6UL,
			0xC63ADCF2CEF25556UL, 0xC5EBD3BEAE9F364FUL, 0xA301D60CF5B6F2FCUL,
			0x8C606CA881D27A00UL, 0x826FEE13B554C18AUL, 0x8DF251716F10B776UL,
			0xB2573A33AC7D94FFUL, 0xC0E771248CB7ABB9UL, 0x753DD605DB38F4EAUL,
			0x21901664C3D92114UL, 0xA408FCB7A1892612UL, 0x3084FC64A03D6722UL,
			0xC8C9D9569AD42A34UL, 0x1FBFBAFC1694B383UL, 0x1894280CC3F94ABEUL,
			0xE14C38A7BBB54651UL, 0x23A48CC84A6EB704UL, 0xD034ADC45AABEDBDUL,
			0xC93F2C21C973C766UL, 0x66A8AEC11D743CC6UL, 0xB4F72AA52E37C145UL,
			0xB02834DF0D9266B4UL, 0xDB8E724EA1FF402FUL, 0x531E9C058112E352UL,
			0xC2F692531DB317D2UL, 0xEFC9586498D263A7UL, 0x84F2C524D2F3ADB9UL,
			0xAFAF02C27CF25D08UL, 0x385873595F9CFC09UL, 0x36DDA10D1A152B7AUL,
			0x9F9B997A0DACFB55UL, 0x10AB5EB5C4714882UL, 0x7BA4E8703E22B7EEUL,
			0x0A2BFD558607BCC8UL, 0x201D3706F74F8BA1UL, 0x3DBD573B1358F02EUL,
			0x5B37645FA93BCEBCUL, 0xC0166864BC1A7544UL, 0x45C7AA5559FC65D7UL,
			0xEFEA04AA83349B78UL, 0x607859194F9E9FD8UL, 0xA6B9AE5B53CF7710UL,
			0x73B9142ACBC50821UL, 0x8B7D67495887E65CUL, 0x39B6C4FB2B232E56UL,
			0xD212DB10E31D2A68UL, 0x629AC0A3D263DC6EUL, 0x6BC2E7FF912050BAUL,
			0xE0AD5A8FDB183F62UL, 0xF05648134F0C6F0FUL, 0x31E146F4AF980FDAUL,
			0x7FAF0078D84D62CCUL, 0xE13F044C2830D21EUL, 0x49A047AD204B4C4BUL,
			0xF3AFBE2237351A74UL, 0x32826C9217BB07EDUL, 0xD4C3AEB099319B5CUL,
			0x49CE5BD05B2B0F61UL, 0x75DD36984DCBD0A2UL, 0x84EC5D7C2F0AAC6CUL,
			0x8E59CC9B9942EDDFUL, 0x89FF85DCDF9AE895UL, 0x6F9EE0D8D9E8D414UL,
			0x10E01A59058D3904UL, 0x1DFAF567BFF55D2EUL, 0x8DD6A18C03382CD4UL,
			0xE12FD89A0CF58553UL, 0xE245DA902C0C4F5CUL, 0x8BE7566B9987520DUL,
			0x4CA1C0A4B38A3098UL, 0x81E45015BE618A72UL, 0xA80E0344FF27EFDFUL,
			0xC98DAEC6DC5005BAUL, 0xF56873F3A958AE5EUL, 0xDB88604670C794ACUL,
			0x4F68E448DDF6535FUL, 0x3443DBF1CA6031A8UL, 0x73DFA5DEEF409A41UL,
			0xA7C556941F6643B2UL, 0x424BC40D8C83D962UL, 0x6F292A325B99B726UL,
			0x6EECB1009717D65EUL, 0x899BE4CE7BB2D8EEUL, 0x25285FED3E59781DUL,
			0x14C5AEDD76E092D3UL, 0x9BB5EE10567640AEUL, 0xCD62A1D43558FD06UL,
			0x70A7B09FC5F39447UL, 0xF10064AE92EFFB99UL, 0xD55FA1A918A23082UL,
			0xD03F28AD25C73A78UL, 0x76CFFFEE094D8B0EUL, 0x4FD5A46AD5A4B4CFUL,
			0x8F3A36F9D7BF87E3UL, 0x64224315210625BEUL, 0x749A131B71B64350UL,
			0x9034FF9DAC089F48UL, 0xB58D3017E7321217UL, 0x549D818937D5CE90UL,
			0x903CE1452419E99CUL, 0xFD052F0388DB2E76UL, 0x7390051E3972480EUL,
			0x5E5F6EC3F27B3679UL, 0x3E3637D4D4EE917DUL, 0x4FE04068CA2A4309UL,
			0x98C9C17454AAE42DUL, 0x659AE0BDB113BC21UL, 0x4C0BDECB1511AF4CUL,
			0x17048BFAEAC0006DUL, 0x68F106AADAA64912UL, 0x2286234ECEB7EAF0UL,
			0x350CD42CAF697E51UL, 0x8DCDE6D1FAC19A9FUL, 0xF97E55A245A8A8A2UL,
			0xAAE86B2092DA90A3UL, 0x5123E878AA8AEF76UL, 0x022B88B9694A55F6UL,
			0xC4C1A9B1C0221985UL, 0x20056D91DD5E0FFEUL, 0xF5BF61EC225C9843UL,
			0x1A315A05BDCF4A31UL, 0x5710A21A8DF4F15FUL, 0x99BD1A0AF97AD027UL,
			0x7602C5997AD4E12CUL
		};

		public static bool IsPopularPassword(char[] vPassword)
		{
			Debug.Assert(PpcTable.Length == (PpcTableSize / 64));

			if(vPassword == null) throw new ArgumentNullException("vPassword");
			if(vPassword.Length == 0) return false;

			foreach(char ch in vPassword)
			{
				if(!IsPopularChar(ch)) return false;
			}

			byte[] pbUtf8 = StrUtil.Utf8.GetBytes(vPassword);

			int[] vIndices = GetTableIndices(pbUtf8, PpcTableSize);
			Array.Clear(pbUtf8, 0, pbUtf8.Length);

			foreach(int iIndex in vIndices)
			{
				if(!GetTableBit(PpcTable, iIndex)) return false;
			}

			return true;
		}

		private static bool IsPopularChar(char ch)
		{
			return (((ch >= 'A') && (ch <= 'Z')) || ((ch >= 'a') && (ch <= 'z')) ||
				((ch >= '0') && (ch <= '9')) || (ch == '_') || (ch == '!'));
		}

		private static int[] GetTableIndices(byte[] pbPasswordUtf8, int nTableSize)
		{
			Debug.Assert((nTableSize >= 2) && (nTableSize <= 0x10000));
			Debug.Assert((nTableSize % 64) == 0);

			SHA512Managed sha = new SHA512Managed();
			byte[] pbHash = sha.ComputeHash(pbPasswordUtf8);

			int[] vIndices = new int[pbHash.Length / 2];
			for(int i = 0; i < vIndices.Length; ++i)
				vIndices[i] = ((((int)pbHash[i * 2] << 8) |
					(int)pbHash[i * 2 + 1]) % nTableSize);

			return vIndices;
		}

		private static bool GetTableBit(ulong[] vTable, int iBit)
		{
			return ((vTable[iBit >> 6] & (1UL << (iBit & 0x3F))) != 0UL);
		}

#if (DEBUG && !KeePassLibSD)
		private static bool SetTableBit(ulong[] vTable, int iBit)
		{
			if(GetTableBit(vTable, iBit)) return false;

			vTable[iBit >> 6] = (vTable[iBit >> 6] | (1UL << (iBit & 0x3F)));
			return true;
		}

		public static void MakeList()
		{
			string strData = File.ReadAllText("MostPopularPasswords.txt", StrUtil.Utf8);
			strData += " ";
			CharStream cs = new CharStream(strData);

			List<string> vPasswords = new List<string>();
			StringBuilder sbPassword = new StringBuilder();
			while(true)
			{
				char ch = cs.ReadChar();
				if(ch == char.MinValue) break;

				if(char.IsWhiteSpace(ch))
				{
					string strPassword = sbPassword.ToString();
					strPassword = strPassword.ToLower();

					if(strPassword.Length > 3)
					{
						if(vPasswords.IndexOf(strPassword) < 0)
							vPasswords.Add(strPassword);
					}

					sbPassword = new StringBuilder();
				}
				else
				{
					Debug.Assert(!char.IsControl(ch) && !char.IsHighSurrogate(ch) &&
						!char.IsLowSurrogate(ch) && !char.IsSurrogate(ch));
					Debug.Assert(IsPopularChar(ch));
					sbPassword.Append(ch);
				}
			}

			ulong[] vTable = new ulong[PpcTableSize / 64];
			Array.Clear(vTable, 0, vTable.Length);

			long lBitsInTable = 0;
			foreach(string strPassword in vPasswords)
			{
				byte[] pbUtf8 = StrUtil.Utf8.GetBytes(strPassword);
				int[] vIndices = GetTableIndices(pbUtf8, PpcTableSize);

				foreach(int i in vIndices)
				{
					if(SetTableBit(vTable, i)) ++lBitsInTable;
				}
			}

			StringBuilder sb = new StringBuilder();
			sb.Append("\t\t\t");
			for(int i = 0; i < vTable.Length; ++i)
			{
				if(i > 0)
				{
					if((i % 3) == 0)
					{
						sb.AppendLine(",");
						sb.Append("\t\t\t");
					}
					else sb.Append(", ");
				}

				sb.Append("0x");
				sb.Append(vTable[i].ToString("X16"));
				sb.Append("UL");
			}

			sb.AppendLine();
			sb.AppendLine();
			sb.AppendLine("Bits set: " + lBitsInTable.ToString() + " of " +
				PpcTableSize.ToString());
			int cHashFn = GetTableIndices(StrUtil.Utf8.GetBytes("Dummy"), PpcTableSize).Length;
			sb.AppendLine("Hash functions: " + cHashFn.ToString());
			double dblPhi = Math.Pow(1.0 - ((double)cHashFn / PpcTableSize),
				(double)vPasswords.Count);
			sb.AppendLine("Phi (bits unset ratio) estimation: " +
				dblPhi.ToString(CultureInfo.InvariantCulture));
			dblPhi = ((double)(PpcTableSize - lBitsInTable) / (double)PpcTableSize);
			sb.AppendLine("Exact Phi: " + dblPhi.ToString(CultureInfo.InvariantCulture));
			sb.AppendLine("False positives ratio: " + Math.Pow(1.0 - dblPhi,
				(double)cHashFn).ToString(CultureInfo.InvariantCulture));

			File.WriteAllText("Table.txt", sb.ToString());
		}
#endif
	}
}
#endif
