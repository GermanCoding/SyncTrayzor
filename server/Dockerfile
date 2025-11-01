FROM php:8.4-apache@sha256:d61f118bb2d52f2ddfbd160972f42bf794a6c605dcd1786cbf3dafdb924e592d
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80