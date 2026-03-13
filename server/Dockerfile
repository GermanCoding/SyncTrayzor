FROM php:8.5-apache@sha256:0a7f9449974d568e1861e29387bacfa8f484e8f113a121f745a6e550ef26c06b
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80