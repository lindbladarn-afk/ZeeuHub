// Handles the editable Purchase order form: autocomplete, row cloning and totals.
(function (window, document, $) {
    'use strict';

    if (!$) {
        return;
    }

    var endpoints = {
        suppliers: '/Purchase/AutoCompleteSupplier/',
        articles: '/Purchase/AutoCompleteArticleName/'
    };

    function postAutocomplete(url, term, response) {
        $.ajax({
            url: url,
            data: JSON.stringify({ searchString: term }),
            dataType: 'json',
            type: 'POST',
            contentType: 'application/json; charset=utf-8',
            success: function (data) {
                response($.map(data, function (item) {
                    return item;
                }));
            },
            error: function (xhr) {
                alert(xhr.responseText);
            },
            failure: function (xhr) {
                alert(xhr.responseText);
            }
        });
    }

    function bindSupplierAutocomplete() {
        var $supplierName = $('#txtSupplierName');
        if (!$supplierName.length || typeof $supplierName.autocomplete !== 'function') {
            return;
        }

        $supplierName.autocomplete({
            source: function (request, response) {
                postAutocomplete(endpoints.suppliers, request.term, response);
            },
            select: function (event, ui) {
                var item = ui.item;

                $('#hfSupplierNumber').val(item.supplierNumber);
                $('#txtSupplierCo').val(item.supplierCo);
                $('#txtSupplierStreet').val(item.supplierStreet);
                $('#txtSupplierZipCode').val(item.supplierZipCode);
                $('#txtSupplierCity').val(item.supplierCity);
                $('#txtSupplierCountry').val(item.supplierCountry);
                $('#txtSupplierCurrency').val(item.currency);
                $('#txtDeliveryCompany').val(item.deliveryCompany);
                $('#txtDeliveryCo').val(item.deliveryCo);
                $('#txtDeliveryStreet').val(item.deliveryStreet);
                $('#txtDeliveryZipCode').val(item.deliveryZipCode);
                $('#txtDeliveryCity').val(item.deliveryCity);
                $('#txtDeliveryCountry').val(item.deliveryCountry);

                bindSupplierContacts(item.contacts || []);
            },
            minLength: 1
        }).focus(function () {
            $(this).autocomplete('search');
        });
    }

    function bindSupplierContacts(contacts) {
        var selectList = $('#selSupplierContact');
        selectList.empty();

        $.each(contacts, function () {
            var option = document.createElement('option');
            option.value = this.contactNumber;
            option.text = this.contactName + ' (' + this.contactNumber + ')';
            selectList.append(option);
        });
    }

    function bindArticleAutocomplete(rowIndex) {
        var selectors = getRowSelectors(rowIndex);
        var $description = $(selectors.description);

        if (!$description.length || typeof $description.autocomplete !== 'function') {
            return;
        }

        $description.autocomplete({
            source: function (request, response) {
                postAutocomplete(endpoints.articles, request.term, response);
            },
            select: function (event, ui) {
                var item = ui.item;

                $(selectors.number).val(item.articleNumber);
                $(selectors.description).val(item.articleDescription);
                $(selectors.unit).val(item.unit);
                $(selectors.account).val(item.defaultAccount);
                $(selectors.costCenter).val(item.defaultCostCenter);
            },
            minLength: 1
        }).focus(function () {
            $(this).autocomplete('search');
        });
    }

    function getRowSelectors(rowIndex) {
        return {
            number: '#OrderRows_' + rowIndex + '__ArticleNumber',
            description: '#OrderRows_' + rowIndex + '__ArticleDescription',
            unit: '#OrderRows_' + rowIndex + '__Unit',
            account: '#OrderRows_' + rowIndex + '__Account',
            costCenter: '#OrderRows_' + rowIndex + '__CostCenter'
        };
    }

    function bindDatePickers() {
        if (!$.fn || typeof $.fn.datepicker !== 'function') {
            return;
        }

        $('.po-datepicker-input').each(function () {
            var input = this;
            var $input = $(input);

            if ($input.hasClass('hasDatepicker')) {
                return;
            }

            var currentValue = input.value;
            input.setAttribute('type', 'text');
            input.setAttribute('autocomplete', 'off');
            input.setAttribute('placeholder', 'YYYY-MM-DD');

            $input.datepicker({
                dateFormat: 'yy-mm-dd',
                showOtherMonths: true,
                selectOtherMonths: true,
                showButtonPanel: true,
                beforeShow: function () {
                    window.setTimeout(function () {
                        $('#ui-datepicker-div').addClass('flowengine-datepicker');
                    }, 0);
                }
            });

            if (currentValue) {
                $input.datepicker('setDate', currentValue);
            }
        });
    }

    function calculateOrderTotal() {
        var table = document.getElementById('articleRows');
        var rows = table ? table.getElementsByTagName('tr') : [];
        var sum = 0;

        for (var index = 0; index < rows.length; index++) {
            var rowValue = document.getElementById(index + '_Total');
            if (isNaN(rowValue?.textContent)) {
                continue;
            }

            sum += Number(rowValue.textContent);
        }

        return sum;
    }

    function updateOrderTotal() {
        var totalSum = calculateOrderTotal();
        var formattedTotal = totalSum.toFixed(2).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        var orderTotalHeader = document.getElementById('OrderTotalHeader');
        var orderTotalFooter = document.getElementById('OrderTotalFooter');

        if (orderTotalHeader) {
            orderTotalHeader.textContent = formattedTotal;
        }

        if (orderTotalFooter) {
            orderTotalFooter.textContent = formattedTotal;
        }
    }

    window.DeleteOrderRow = function (btn) {
        $(btn).closest('tr').remove();
        updateOrderTotal();
    };

    window.CalculateRowTotal = function (field) {
        var index = field.id.split('_')[0];
        var quantity = document.getElementById(index + '_Quantity').value;
        var price = document.getElementById(index + '_Price').value;
        var discount = document.getElementById(index + '_Discount').value;
        var rowTotal = document.getElementById(index + '_Total');
        var rowTotalSum = 0;

        if (isNaN(quantity) || isNaN(price) || isNaN(discount)) {
            rowTotal.textContent = 'N/A';
            updateOrderTotal();
            return;
        }

        if ((quantity > 0) && (price > 0)) {
            if ((discount > 0) && (discount <= 100)) {
                rowTotalSum = (price * ((100 - discount) / 100)) * quantity;
            } else if (Number(discount) === 0) {
                rowTotalSum = price * quantity;
            } else {
                rowTotalSum = 'N/A';
            }
        }

        rowTotal.textContent = typeof rowTotalSum === 'number' ? rowTotalSum.toFixed(2) : rowTotalSum;
        updateOrderTotal();
    };

    window.CalculateOrderTotal = calculateOrderTotal;

    window.AddOrderRow = function (btn) {
        var table = document.getElementById('articleRows');
        var rows = table.getElementsByTagName('tr');
        var rowOuterHtml = rows[rows.length - 1].outerHTML;
        var lastRowIndex = document.getElementById('hdnLastIndex').value;
        var nextRowIndex = Number(lastRowIndex) + 1;

        document.getElementById('hdnLastIndex').value = nextRowIndex;

        rowOuterHtml = rowOuterHtml.replaceAll('_' + lastRowIndex + '_', '_' + nextRowIndex + '_');
        rowOuterHtml = rowOuterHtml.replaceAll('[' + lastRowIndex + ']', '[' + nextRowIndex + ']');
        rowOuterHtml = rowOuterHtml.replaceAll('-' + lastRowIndex, '-' + nextRowIndex);
        rowOuterHtml = rowOuterHtml.replaceAll(lastRowIndex + '_', nextRowIndex + '_');

        var newRow = table.insertRow();
        newRow.innerHTML = rowOuterHtml;

        var newRowTotal = document.getElementById(nextRowIndex + '_Total');
        if (newRowTotal) {
            newRowTotal.textContent = '0';
        }

        var btnAddId = btn.id;
        var btnAdd = document.getElementById(btnAddId);
        var btnRemoveId = btnAddId.replaceAll('btnAdd', 'btnRemove');
        var btnRemove = document.getElementById(btnRemoveId);

        bindArticleAutocomplete(nextRowIndex);

        btnRemove.style.display = 'block';
        btnAdd.style.display = 'none';
    };

    window.AddAutoComplete = bindArticleAutocomplete;

    $(function () {
        bindSupplierAutocomplete();
        bindArticleAutocomplete(0);
        bindDatePickers();
    });
})(window, document, window.jQuery);
