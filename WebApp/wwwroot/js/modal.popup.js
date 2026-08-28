


$(document).ready(function () {
    var flashStorageKey = 'admin.users.create.success';
    var placeHolderElement = $('#ModalPopupPlaceholder');
    $('button[data-toggle="ajax-modal"]').click(function (event) {
        var url = $(this).data('url');
        $.get(url).done(function (data) {
            placeHolderElement.html(data);
            placeHolderElement.find('.modal').modal('show');
        })
    })

    placeHolderElement.on('click', '[data-save="modal"]', function (event) {
        event.preventDefault();

        var form = $(this).parents('.modal').find('form');
        var actionUrl = form.attr('action');
        var sendData = form.serialize();
        $.post(actionUrl, sendData).done(function (data) {
            if (data && typeof data === 'object' && data.success) {
                if (data.message) {
                    sessionStorage.setItem(flashStorageKey, data.message);
                }

                placeHolderElement.find('.modal').modal('hide');
                window.location.href = data.redirectUrl || window.location.href;
                return;
            }

            if (typeof data === 'string' && data.indexOf('id="createUser"') !== -1) {
                placeHolderElement.html(data);
                placeHolderElement.find('.modal').modal('show');
                return;
            }

            placeHolderElement.find('.modal').modal('hide');
            location.reload(true);
        })
    })

    placeHolderElement.on('click', '[data-dismiss="modal"]', function (event) {
        placeHolderElement.find('.modal').modal('hide');
    })    
})
