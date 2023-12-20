﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ChainFX;
using ChainFX.Web;
using static ChainFX.Entity;
using static ChainFX.Web.Modal;
using static ChainFX.Nodal.Nodality;
using static ChainFX.Web.ToolAttribute;

namespace ChainSmart;

public abstract class ItemWork<V> : WebWork where V : ItemVarWork, new()
{
    protected override void OnCreate()
    {
        CreateVarWork<V>();
    }
}

public class PubItemWork : ItemWork<PublyItemVarWork>
{
}

[Ui("商品")]
public class RtllyItemWork : ItemWork<RtllyItemVarWork>
{
    protected static void MainGrid(HtmlBuilder h, Item[] arr)
    {
        h.MAINGRID(arr, o =>
        {
            h.ADIALOG_(o.Key, "/", MOD_OPEN, false, tip: o.name, css: "uk-card-body uk-flex");
            if (o.icon)
            {
                h.PIC(MainApp.WwwUrl, "/item/", o.id, "/icon", css: "uk-width-1-5");
            }
            else
                h.PIC("/void.webp", css: "uk-width-1-5");

            h.ASIDE_();
            h.HEADER_().H4(o.name);
            if (o.step > 1)
            {
                h.SP().SMALL_().T(o.step).T(o.unit).T("为整")._SMALL();
            }

            h.SPAN(Statuses[o.status], "uk-badge");
            h._HEADER();

            h.Q(o.tip, "uk-width-expand");
            h.FOOTER_().SPAN3("货架", o.stock, o.unit).SPAN_("uk-margin-auto-left").CNY(o.price)._SPAN()._FOOTER();
            h._ASIDE();

            h._A();
        });
    }

    [Ui("上线商品", status: 1), Tool(Anchor)]
    public async Task @default(WebContext wc)
    {
        var org = wc[-1].As<Org>();

        using var dc = NewDbContext();
        dc.Sql("SELECT ").collst(Item.Empty).T(" FROM items_vw WHERE orgid = @1 AND status = 4 ORDER BY oked DESC");
        var arr = await dc.QueryAsync<Item>(p => p.Set(org.id));

        wc.GivePage(200, h =>
        {
            h.TOOLBAR();

            if (arr == null)
            {
                h.ALERT("尚无上线商品");
                return;
            }

            MainGrid(h, arr);
        }, false, 4);
    }

    [Ui(tip: "已下线", icon: "cloud-download", status: 2), Tool(Anchor)]
    public async Task down(WebContext wc)
    {
        var org = wc[-1].As<Org>();

        using var dc = NewDbContext();
        dc.Sql("SELECT ").collst(Item.Empty).T(" FROM items_vw WHERE orgid = @1 AND status BETWEEN 1 AND 2 ORDER BY adapted DESC");
        var arr = await dc.QueryAsync<Item>(p => p.Set(org.id));

        wc.GivePage(200, h =>
        {
            h.TOOLBAR();
            if (arr == null)
            {
                h.ALERT("尚无已下线的商品");
                return;
            }

            MainGrid(h, arr);
        }, false, 4);
    }

    [Ui(tip: "已作废", icon: "trash", status: 4), Tool(Anchor)]
    public async Task @void(WebContext wc)
    {
        var org = wc[-1].As<Org>();

        using var dc = NewDbContext();
        dc.Sql("SELECT ").collst(Item.Empty).T(" FROM items_vw WHERE orgid = @1 AND status = 0 ORDER BY adapted DESC");
        var arr = await dc.QueryAsync<Item>(p => p.Set(org.id));

        wc.GivePage(200, h =>
        {
            h.TOOLBAR();
            if (arr == null)
            {
                h.ALERT("尚无已作废的商品");
                return;
            }

            MainGrid(h, arr);
        }, false, 4);
    }

    [UserAuthorize(Org.TYP_RTL, User.ROL_MGT)]
    [Ui("新建", tip: "创建新的商品信息", icon: "plus", status: 2), Tool(ButtonOpen)]
    public async Task @new(WebContext wc)
    {
        var org = wc[-1].As<Org>();
        var prin = (User)wc.Principal;

        const short MAX = 100;
        var o = new Item
        {
            typ = Item.TYP_NEW,
            orgid = org.id,
            created = DateTime.Now,
            creator = prin.name,
            unit = "斤",
            unitw = 500,
            step = 1,
            min = 1,
            max = MAX
        };
        if (wc.IsGet)
        {
            wc.GivePane(200, h =>
            {
                h.FORM_().FIELDSUL_(wc.Action.Tip);

                h.LI_().TEXT("商品名", nameof(o.name), o.name, max: 12)._LI();
                h.LI_().TEXTAREA("简介语", nameof(o.tip), o.tip, max: 40)._LI();
                h.LI_().SELECT("零售单位", nameof(o.unit), o.unit, Unit.Typs, showkey: true, onchange: "this.form.unitw.value = this.selectedOptions[0].title").SELECT("单位含重", nameof(o.unitw), o.unitw, Unit.Weights)._LI();
                h.LI_().NUMBER("单价", nameof(o.price), o.price, min: 0.01M, max: 99999.99M).NUMBER("整售", nameof(o.step), o.step, min: 1, money: false, onchange: $"this.form.min.value = this.value; this.form.max.value = this.value * {MAX}; ")._LI();
                h.LI_().NUMBER("VIP立减", nameof(o.off), o.off, min: 0.00M, max: 999.99M).CHECKBOX("全民立减", nameof(o.promo), o.promo)._LI();
                h.LI_().NUMBER("起订量", nameof(o.min), o.min, min: 1, max: o.stock).NUMBER("限订量", nameof(o.max), o.max, min: MAX)._LI();

                h._FIELDSUL().BOTTOM_BUTTON("确认", nameof(@new))._FORM();
            });
        }
        else // POST
        {
            const short msk = MSK_BORN | MSK_EDIT;
            // populate 
            var m = await wc.ReadObjectAsync(msk, o);

            // insert
            using var dc = NewDbContext();
            dc.Sql("INSERT INTO items ").colset(Item.Empty, msk)._VALUES_(Item.Empty, msk);
            await dc.ExecuteAsync(p => m.Write(p, msk));

            wc.GivePane(200); // close dialog
        }
    }

    [UserAuthorize(Org.TYP_RTL, User.ROL_MGT)]
    [Ui("导入", "导入已采购的供应链产品", icon: "plus", status: 2), Tool(ButtonOpen)]
    public async Task imp(WebContext wc)
    {
        var org = wc[-1].As<Org>();
        var prin = (User)wc.Principal;

        const short MAX = 100;
        var o = new Item
        {
            typ = Item.TYP_IMP,
            created = DateTime.Now,
            creator = prin.name,
            step = 1,
            min = 1,
            max = MAX
        };

        if (wc.IsGet)
        {
            using var dc = NewDbContext();
            dc.Sql("SELECT DISTINCT lotid, concat(name, ' ￥', (price - off)::decimal ), id FROM purs WHERE rtlid = @1 AND status = 4 ORDER BY id DESC LIMIT 50");
            await dc.QueryAsync(p => p.Set(org.id));
            var lots = dc.ToIntMap();

            wc.GivePane(200, h =>
            {
                h.FORM_().FIELDSUL_(wc.Action.Tip);

                h.LI_().SELECT("供应产品名", nameof(o.lotid), o.lotid, lots, required: true)._LI();
                h.LI_().NUMBER("单价", nameof(o.price), o.price, min: 0.01M, max: 99999.99M).NUMBER("为整", nameof(o.step), o.step, min: 1, money: false, onchange: $"this.form.min.value = this.value; this.form.max.value = this.value * {MAX}; ")._LI();
                h.LI_().NUMBER("VIP立减", nameof(o.off), o.off, min: 0.00M, max: 999.99M).CHECKBOX("全民立减", nameof(o.promo), o.promo)._LI();
                h.LI_().NUMBER("起订量", nameof(o.min), o.min, min: 1, max: o.stock).NUMBER("限订量", nameof(o.max), o.max, min: MAX)._LI();

                h._FIELDSUL().BOTTOM_BUTTON("确认", nameof(imp))._FORM();
            });
        }
        else // POST
        {
            const short msk = MSK_BORN | MSK_EDIT;
            // populate 
            await wc.ReadObjectAsync(msk, o);

            using var dc = NewDbContext(IsolationLevel.ReadCommitted);

            dc.Sql("SELECT ").collst(Lot.Empty).T(" FROM lots_vw WHERE id = @1");
            var lot = await dc.QueryTopAsync<Lot>(p => p.Set(o.lotid));
            // init by lot
            {
                o.name = lot.name;
                o.tip = lot.tip;
                o.unit = lot.unit;
                o.unitw = lot.unitw;
            }

            // insert
            dc.Sql("INSERT INTO items ").colset(Item.Empty, msk)._VALUES_(Item.Empty, msk).T(" RETURNING id");
            var itemid = (int)await dc.ScalarAsync(p => o.Write(p, msk));

            dc.Sql("UPDATE items SET (icon, pic) = (SELECT icon, pic FROM lots WHERE id = @1) WHERE id = @2");
            await dc.ExecuteAsync(p => p.Set(o.lotid).Set(itemid));

            wc.GivePane(200); // close dialog
        }
    }

    [UserAuthorize(Org.TYP_RTL, User.ROL_MGT)]
    [Ui("清空", "永久删除已作废的数据项", status: 4), Tool(ButtonConfirm)]
    public async Task empty(WebContext wc)
    {
        var org = wc[-1].As<Org>();

        using var dc = NewDbContext();

        dc.Sql("DELETE FROM items WHERE orgid = @1 AND status = 0");
        await dc.ExecuteAsync(p => p.Set(org.id));

        wc.GivePane(200); // close dialog
    }
}